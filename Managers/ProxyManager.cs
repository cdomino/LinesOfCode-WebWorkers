using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using LinesOfCode.Web.Workers.Models;
using LinesOfCode.Web.Workers.Utilities;
using LinesOfCode.Web.Workers.Contracts;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This handles calling .NET services from a web worker.
    /// </summary>
    public class ProxyManager : ComponentBase, IDisposable
    {
        #region Members
        private DotNetObjectReference<ProxyManager> _jsReference = null;
        private Dictionary<Guid, ProxyModel> _proxies = new Dictionary<Guid, ProxyModel>();
        #endregion
        #region Dependencies
        [Inject()]
        private IJSRuntime _jsRuntime { get; set; }

        [Inject()]
        private IServiceProvider _container { get; set; }

        [Inject()]
        private ILogger<ProxyManager> _logger { get; set; }

        [Inject()]
        private ISerializationManager _serializationManager { get; set; }

        [Inject()]
        private IMemoryCacheManager<Type, MethodInfo> _marshallerCache { get; set; }
        #endregion       
        #region Events
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            //initialization
            if (firstRender)
            {
                //return
                this._jsReference = DotNetObjectReference.Create(this);
                await this._jsRuntime.InvokeVoidAsync(WebWorkerConstants.JavaScript.ConnectWebWorker, this._jsReference, nameof(this.RunMethodAsync), nameof(this.SetAuthenticationTokenAsync));
            }
        }
        #endregion
        #region Public Methods
        public void Dispose()
        {
            //return
            this._jsReference?.Dispose();
        }

        /// <summary>
        /// To avoid copious serialization, this strategy uses flags and direct casting to invoke well-known .NET methods from web workers.
        /// </summary>
        [JSInvokable()]
        public async Task<object> RunMethodAsync(Guid invocationId, ProxyModel model, Dictionary<string, string> eventRegistrationData)
        {
            //initialization            
            string message = $" web worker invocation {invocationId} of {model.MethodName} on {model.InterfaceType}";
            Dictionary<EventInfo, Delegate> eventRegistrations = new Dictionary<EventInfo, Delegate>();
            this._logger.LogInformation($"Running{message}.");
            List<object> parameters = new List<object>();

            //get interface implementation
            Type voidType = typeof(void);
            Type stringType = typeof(string);
            this._proxies.Add(invocationId, model);
            Type interfaceType = Type.GetType(model.InterfaceType);
            object interfaceImplementation = this._container.GetRequiredService(interfaceType);
            this._logger.LogInformation($"Instantiated implemention {interfaceType.Name} for{message}.");

            //get method
            MethodInfo method = interfaceImplementation.GetType().GetMethod(model.MethodName);
            if (model.GenericTypeNames?.Any() ?? false)
                method = method.MakeGenericMethod(model.GenericTypeNames.Select(t => Type.GetType(t)).ToArray());

            //extract parameters
            this._logger.LogInformation($"Extracted method for{message}.");
            foreach (string parameter in model.ParameterValues.Keys)
            {
                //check null values first
                if (model.ParameterValues[parameter] == null)
                {
                    //pass nulls through
                    parameters.Add(null);
                }
                else
                {
                    //get parameter value
                    Type type = Type.GetType(model.ParameterTypeNames[parameter]);
                    string value = model.ParameterValues[parameter].ToString();
                    if (value.IsJSON() && type != stringType)
                    {
                        //complex type
                        parameters.Add(await this._serializationManager.DeserializeAsync(type, value));
                    }
                    else
                    {
                        //primative type
                        parameters.Add(value.ConvertFromString(type));
                    }
                }
            }         

            //check events
            if (eventRegistrationData?.Any() ?? false)
            {
                //register events
                Type thisType = this.GetType();
                Type objectType = typeof(object);
                Type eventArgsType = typeof(EventArgs);
                MethodInfo marshalEventMethod = thisType.GetMethod(nameof(this.MarshalEvent), BindingFlags.NonPublic | BindingFlags.Instance);

                //hook requested events
                foreach (EventInfo eventInfo in interfaceType.GetEvents().Where(e => eventRegistrationData.ContainsKey(e.Name)))
                {
                    //get event metadata
                    string eventName = eventInfo.Name;
                    bool isGenericEvent = eventInfo.EventHandlerType.IsGenericType;
                    Type eventArgumentType = isGenericEvent ? eventInfo.EventHandlerType.GetGenericArguments().Single() : eventArgsType;

                    //get a handler to match this event's signature
                    MethodInfo eventMarshaller = this._marshallerCache.GetOrAdd(eventArgumentType, _ =>
                    {
                        //return
                        return marshalEventMethod.MakeGenericMethod(eventArgumentType);
                    });

                    //build a dynamic method (which will have access to "this") to handle the native event and pass instance variables along with the event metadata to the marshaller
                    DynamicMethod eventHandler = new DynamicMethod(eventName, voidType, new Type[] { objectType, objectType, isGenericEvent ? eventArgumentType : eventArgsType }, thisType.Module, true);
                    ILGenerator il = eventHandler.GetILGenerator();
                    il.DeclareLocal(stringType);
                    il.DeclareLocal(stringType);

                    //pass invocation id
                    il.Emit(OpCodes.Ldstr, invocationId.ToString());
                    il.Emit(OpCodes.Stloc_0);

                    //pass event name
                    il.Emit(OpCodes.Ldstr, eventName);
                    il.Emit(OpCodes.Stloc_1);

                    //pass event argument type
                    il.Emit(OpCodes.Ldstr, isGenericEvent ? eventArgumentType.AssemblyQualifiedName : voidType.AssemblyQualifiedName);
                    il.Emit(OpCodes.Stloc_2);

                    //call delegate
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Ldloc_2);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Call, eventMarshaller);
                    il.Emit(OpCodes.Ret);

                    //register handler
                    this._logger.LogInformation($"Registering web worker event {eventName} with arg of type {eventArgumentType?.AssemblyQualifiedName ?? "N/A"}.");
                    Delegate eventDelegate = eventHandler.CreateDelegate(eventInfo.EventHandlerType, this);
                    eventInfo.AddEventHandler(interfaceImplementation, eventDelegate);
                    eventRegistrations.Add(eventInfo, eventDelegate);
                }
            }

            //prepare method invocation
            bool isAsync = method.ReturnType.IsAsync();
            bool isGeneric = method.ReturnType.IsGenericType;
            this._logger.LogInformation($"Extracted {parameters.Pluralize("parameter")} for{message}.");

            try
            {
                //invoke method
                this._logger.LogInformation($"Invoking method for{message}.");
                object result = method.Invoke(interfaceImplementation, parameters.ToArray());

                //complete async task
                if (isAsync)
                {
                    //if the method is async, get the task and complete it
                    Task task = (Task)result;
                    await task.ConfigureAwait(false);
                    this._logger.LogInformation($"Awaited async method for{message}.");

                    //unpack async result
                    if (isGeneric)
                    {
                        //task<T>
                        PropertyInfo resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
                        result = resultProperty.GetValue(task);

                        //return
                        this._logger.LogTrace($"Got async result {result} for{message}.");
                        return result;
                    }
                    else
                    {
                        //task
                        this._logger.LogInformation($"Completed async void method for{message}.");
                        return null;
                    }
                }
                else
                {
                    //not async
                    if (method.ReturnType == voidType)
                        this._logger.LogInformation($"Completed void method for{message}.");
                    else
                        this._logger.LogInformation($"Got sync result {result} for{message}.");

                    //return
                    return result;
                }
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError($"Unable to complete method invocation {invocationId} of {model.MethodName} on {model.InterfaceType}: {ex}");
                throw;
            }
            finally
            {
                //unhook events
                this._proxies.Remove(invocationId);
                foreach (EventInfo eventRegistration in eventRegistrations.Keys)
                    eventRegistration.RemoveEventHandler(interfaceImplementation, eventRegistrations[eventRegistration]);
            }
        }        

        /// <summary>
        /// This sets the autentication token after a web worker has been DI-ed.
        /// </summary>
        [JSInvokable()]
        public async Task SetAuthenticationTokenAsync(AzureB2CTokenModel token)
        {
            //initialization
            await WebWorkerUtilities.YieldAsync();
            if (token == null)
                throw new ArgumentNullException("The authentication token is required.");

            //return
            ISettingsManager settingsManager = this._container.GetRequiredService<ISettingsManager>();
            settingsManager.Token = token;
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Marshals an event to the main UI thread.
        /// </summary>
        private async void MarshalEvent<T>(string rawInvocationId, string eventName, string eventArgumentTypeName, object sender, T value)
        {
            //initialization
            Guid invocationId = Guid.Parse(rawInvocationId);
            this._logger.LogTrace($"Handling event {eventName} from {sender} for invocation {invocationId} of type {eventArgumentTypeName}.");

            //return
            ProxyModel proxy = this._proxies[invocationId];
            await this._jsRuntime.InvokeVoidAsync(WebWorkerConstants.JavaScript.MarshalEvent, invocationId, proxy, eventName, eventArgumentTypeName, value);
        }
        #endregion
    }
}
