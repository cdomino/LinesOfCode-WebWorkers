using System;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Authorization;

using LinesOfCode.Web.Workers.Models;
using LinesOfCode.Web.Workers.Utilities;
using LinesOfCode.Web.Workers.Enumerations;
using LinesOfCode.Web.Workers.Contracts.Services;
using LinesOfCode.Web.Workers.Contracts.Managers;

using Blazored.SessionStorage;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This provides configuration and lifecycle management for web worker instances.
    /// </summary>
    public class WebWorkerManager : BaseModuleManager<WebWorkerManager>, IWebWorkerManager
    {
        #region Members
        private readonly List<Guid> _webWorkerIds;
        private readonly ILogger<WebWorkerManager> _logger;
        private readonly ISettingsService _settingsService;
        private readonly Lazy<ModuleBuilder> _moduleBuilder;
        private readonly ISerializationService _serializationManager;
        private readonly IMemoryCacheManager<string, Type> _typeCache;
        private readonly ISessionStorageService _sessionStorageService; 
        private readonly IMemoryCacheManager<string, MethodInfo> _handlerCache;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly Dictionary<Guid, List<Func<Guid, Task>>> _creationCallbacks;
        private readonly Dictionary<string, Dictionary<string, Type>> _proxyReturnTypes;
        private readonly Dictionary<Guid, Dictionary<string, object>> _proxyEventCallbacks;
        private readonly Dictionary<string, Dictionary<string, object>> _proxySuccessCallbacks;
        private readonly Dictionary<string, Dictionary<string, Func<ErrorMessageModel, Task>>> _proxyErrorCallbacks;
        #endregion
        #region Initialization
        public WebWorkerManager(IJSRuntime jsRuntime,
                                ISettingsService settingsService,
                                ILogger<WebWorkerManager> logger,
                                ISerializationService serializationManager,
                                IMemoryCacheManager<string, Type> typeCache,
                                ISessionStorageService sessionStorageService,                                
                                IMemoryCacheManager<string, MethodInfo> handlerCache,
                                AuthenticationStateProvider authenticationStateProvider) : base(jsRuntime, WebWorkerConstants.JavaScriptInterop.Modules.WebWorkerManager.ImportPath)
        {
            //initialization
            this._webWorkerIds = new List<Guid>();
            this._creationCallbacks = new Dictionary<Guid, List<Func<Guid, Task>>>();
            this._proxyReturnTypes = new Dictionary<string, Dictionary<string, Type>>();
            this._proxyEventCallbacks = new Dictionary<Guid, Dictionary<string, object>>();
            this._proxySuccessCallbacks = new Dictionary<string, Dictionary<string, object>>();
            this._proxyErrorCallbacks = new Dictionary<string, Dictionary<string, Func<ErrorMessageModel, Task>>>();

            //ensure dependencies
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._typeCache = typeCache ?? throw new ArgumentNullException(nameof(typeCache));            
            this._handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));
            this._settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this._serializationManager = serializationManager ?? throw new ArgumentNullException(nameof(serializationManager));
            this._sessionStorageService = sessionStorageService ?? throw new ArgumentNullException(nameof(sessionStorageService));
            this._authenticationStateProvider = authenticationStateProvider ?? throw new ArgumentNullException(nameof(authenticationStateProvider));

            //return
            this._moduleBuilder = new Lazy<ModuleBuilder>(() =>
            {
                //initialization
                AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);

                //only build module once
                return assemblyBuilder.DefineDynamicModule(nameof(WebWorkerManager));
            });
        }
        #endregion
        #region Properties
        public Guid[] WebWorkerIds => this._webWorkerIds.ToArray();
        #endregion
        #region Public Methods
        /// <summary>
        /// Creates a new instance of a web worker.
        /// </summary>
        public async Task<CreateWorkerCallbackStatus> CreateWorkerAsync(Guid? workerId = null, Func<Guid, Task> createdCallback = null)
        {
            //initialization
            Guid id = workerId.GetValueOrDefault(Guid.NewGuid());
            this._logger.LogInformation($"Creating a new web worker with id {id}.");

            //check existing
            if (this._webWorkerIds.Contains(id))
            {
                //worker already exists
                this._logger.LogInformation($"Web worker {id} has already been created.");
                return CreateWorkerCallbackStatus.AlreadyExists;
            }
            else if (this._creationCallbacks.ContainsKey(id))
            {
                //worker is being created
                this._logger.LogWarning($"Web worker {id} is still being initialized.");
                return CreateWorkerCallbackStatus.AlreadyInitializing;
            }

            //create worker
            IJSObjectReference module = await this.GetModuleAsync();
            AzureB2CTokenModel token = await this.GetB2CTokenAsync();
            await module.InvokeVoidAsync(WebWorkerConstants.JavaScriptInterop.Functions.CreateWebWorker, id, this._jsReference, nameof(this.WorkerCreatedAsync), nameof(this.WebWorkerFinishedAsync), nameof(this.WebWorkerFailedAsync), nameof(this.WebWorkerEventRaisedAsync), nameof(this.GetB2CTokenAsync), this._settingsService.GetAllSettings(), token);

            //register callback
            this._creationCallbacks.Add(id, new List<Func<Guid, Task>>() { createdCallback });

            //return
            return CreateWorkerCallbackStatus.Initializing;
        }

        /// <summary>
        /// Sends an auth token to a web worker. Returns false if a token wasn't found.
        /// </summary>
        public async Task<bool> SendAuthenticationTokenToWebWorkerAsync(Guid workerId)
        {
            //initialization
            AzureB2CTokenModel token = await this.GetB2CTokenAsync();
            if (token == null)
            {
                //error
                this._logger.LogWarning($"Cannot send a null token to web worker {workerId}.");
                return false;
            }
            else
            {
                //send token
                IJSObjectReference module = await this.GetModuleAsync();
                await module.InvokeVoidAsync(WebWorkerConstants.JavaScriptInterop.Functions.SendWebWorkerToken, workerId, token);

                //return
                return true;
            }
        }

        /// <summary>
        /// Allows a caller to register an additional callback for a web worker.
        /// </summary>
        public async Task<AdditionalWorkerCallbackStatus> RegisterWorkerCreationCallbackAsync(Guid workerId, Func<Guid, Task> createdCallbackAsync)
        {
            //initialization
            if (this._creationCallbacks.ContainsKey(workerId))
            {
                //callback registered
                this._logger.LogInformation($"Registered callback for web worker {workerId}.");
                this._creationCallbacks[workerId].Add(createdCallbackAsync);

                //return
                return AdditionalWorkerCallbackStatus.Registered;
            }
            else if (this._webWorkerIds.Contains(workerId))
            {
                //call method now
                this._logger.LogInformation($"Since web worker {workerId} is already created, invoking callback now.");
                await createdCallbackAsync(workerId);

                //return
                return AdditionalWorkerCallbackStatus.Executed; 
            }
            else
            {
                //worker not found
                this._logger.LogWarning($"Unable to register callback for web worker {workerId} as it was not found.");
                return AdditionalWorkerCallbackStatus.NotFound;
            }
        }

        /// <summary>
        /// Immediately terminates a web worker.
        /// </summary>
        public async Task TerminateWorkerAsync(Guid workerId)
        {
            //initialization
            IJSObjectReference module = await this.GetModuleAsync();
            this._logger.LogInformation($"Terminated web worker {workerId}.");

            //return
            await module.InvokeVoidAsync(WebWorkerConstants.JavaScriptInterop.Functions.TerminateWebWorker, workerId);
            this._creationCallbacks.Remove(workerId);
            this._webWorkerIds.Remove(workerId);
        }

        /// <summary>
        /// Returns a new interface that proxies all method invocations to a JavaScript web worker.
        /// </summary>
        public async Task<I> GetProxyImplementationAsync<I>(Guid webWorkerId, string fileUploadControlId = null)
        {
            //initialization           
            Type interfaceType = typeof(I);
            if (!interfaceType.IsInterface)
                throw new ArgumentException($"Cannot proxy {interfaceType.Name}: the type must be an interface.");

            //get module
            IJSObjectReference module = await this.GetModuleAsync();
            fileUploadControlId = fileUploadControlId ?? string.Empty;
            string proxyTypeName = $"{interfaceType.FullName}.{nameof(WebWorkerConstants.Proxy)}";

            //check type cache
            Type proxyType = this._typeCache.GetOrAdd(proxyTypeName, (_) =>
            {
                //define common types
                Type voidType = typeof(void);
                Type typeType = typeof(Type);
                Type guidType = typeof(Guid);
                Type thisType = this.GetType();
                Type objectType = typeof(object);
                Type stringType = typeof(string);
                Type typeArrayType = typeof(Type[]);
                Type delegateType = typeof(Delegate);
                Type objectArrayType = typeof(object[]);
                Type stringArrayType = typeof(string[]);
                Type jsModuleType = typeof(IJSObjectReference);
                Type dictionaryStringStringType = typeof(Dictionary<string, string>);

                //build a type that implements the given interface
                TypeBuilder typeBuilder = this._moduleBuilder.Value.DefineType(proxyTypeName, TypeAttributes.Public);
                typeBuilder.AddInterfaceImplementation(interfaceType);

                //ensure proxy return type
                if (!this._proxyReturnTypes.ContainsKey(interfaceType.AssemblyQualifiedName))
                    this._proxyReturnTypes.Add(interfaceType.AssemblyQualifiedName, new Dictionary<string, Type>());

                //create fields to hold the JS runtime and worker id
                FieldBuilder webWorkerIdField = typeBuilder.DefineField(WebWorkerConstants.Proxy.WebWorkerIdFieldName, guidType, FieldAttributes.Private);
                FieldBuilder jsModuleField = typeBuilder.DefineField(WebWorkerConstants.Proxy.JavaScriptModuleFieldName, jsModuleType, FieldAttributes.Private);
                FieldBuilder fileControlIdField = typeBuilder.DefineField(WebWorkerConstants.Proxy.FileControlIdFieldName, stringType, FieldAttributes.Private);

                //create a constructor
                ConstructorBuilder constuctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { jsModuleType, guidType, stringType });
                ILGenerator constructorIL = constuctor.GetILGenerator();

                //store JS runtime
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_1);
                constructorIL.Emit(OpCodes.Stfld, jsModuleField);

                //store web worker id
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_2);
                constructorIL.Emit(OpCodes.Stfld, webWorkerIdField);

                //store file control id
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_3);
                constructorIL.Emit(OpCodes.Stfld, fileControlIdField);

                //create fields to store invocation-specific values
                constructorIL.Emit(OpCodes.Ret);
                typeBuilder.DefineField(WebWorkerConstants.Proxy.InvocationIdFieldName, guidType, FieldAttributes.Private);
                typeBuilder.DefineField(WebWorkerConstants.Proxy.EventRegistrationsFieldName, dictionaryStringStringType, FieldAttributes.Private);

                //reference helper methods that are called by the IL
                MethodInfo getTypeFromHandle = typeType.GetMethod(nameof(Type.GetTypeFromHandle));
                MethodInfo proxyMethodVoidSync = thisType.GetMethod(nameof(this.ProxyMethodVoidSync), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo proxyMethodVoidAsync = thisType.GetMethod(nameof(this.ProxyMethodVoidAsync), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo proxyMethodReturnSync = thisType.GetMethod(nameof(this.ProxyMethodReturnSync), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo proxyMethodReturnAsync = thisType.GetMethod(nameof(this.ProxyMethodReturnAsync), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo eventAddMethod = delegateType.GetMethod(WebWorkerConstants.Proxy.DynamicEventAddMethod, new Type[] { delegateType, delegateType });
                MethodInfo eventRemoveMethod = delegateType.GetMethod(WebWorkerConstants.Proxy.DynamicEventRemoveMethod, new Type[] { delegateType, delegateType });

                //reference method attributes
                MethodImplAttributes eventMethodImplementationAttributes = MethodImplAttributes.Managed | MethodImplAttributes.Synchronized;
                MethodAttributes propertyAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
                MethodAttributes eventMethodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.SpecialName;

                //get all methods defined on the interface
                foreach (MethodInfo methodToProxy in interfaceType.GetMethods())
                {
                    //skip events, properties, etc.
                    if (methodToProxy.IsSpecialName)
                        continue;

                    //get parameter info for each method
                    ParameterInfo[] parameters = methodToProxy.GetParameters();
                    Type[] parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
                    this._logger.LogInformation($"Starting proxy generation for method {methodToProxy} with {parameters.Pluralize("parameter")}.");

                    //start method proxy generation
                    MethodBuilder dynamicMethod = typeBuilder.DefineMethod(methodToProxy.Name, MethodAttributes.Public | MethodAttributes.Virtual, methodToProxy.ReturnType, parameterTypes);
                    ILGenerator methodIL = dynamicMethod.GetILGenerator();
                    bool isVoid = methodToProxy.ReturnType == voidType;
                    bool isAsync = methodToProxy.ReturnType.IsAsync();
                    MethodInfo implementationMethod = null;

                    //determine which concrete method to use based on voidness
                    if (isAsync)
                    {
                        //determine if the task has a generic return type
                        if (methodToProxy.ReturnType.IsGenericType)
                            implementationMethod = proxyMethodReturnAsync.MakeGenericMethod(methodToProxy.ReturnType.GetGenericArguments().First());
                        else
                            implementationMethod = proxyMethodVoidAsync;
                    }
                    else if (isVoid)
                    {
                        //special case for void
                        implementationMethod = proxyMethodVoidSync;
                    }
                    else
                    {
                        //sync method with return type
                        implementationMethod = proxyMethodReturnSync.MakeGenericMethod(methodToProxy.ReturnType);
                    }

                    //declare variables
                    methodIL.DeclareLocal(typeType);
                    methodIL.DeclareLocal(stringType);
                    methodIL.DeclareLocal(typeType);
                    methodIL.DeclareLocal(typeArrayType);
                    methodIL.DeclareLocal(stringArrayType);
                    methodIL.DeclareLocal(typeArrayType);
                    methodIL.DeclareLocal(objectArrayType);

                    //store interface type at variable index 0
                    methodIL.PushTypeToStack(interfaceType, getTypeFromHandle);
                    methodIL.Emit(OpCodes.Stloc_0);

                    //store method name at variable index 1
                    methodIL.Emit(OpCodes.Ldstr, methodToProxy.Name);
                    methodIL.Emit(OpCodes.Stloc_1);

                    //store return type at variable index 2
                    methodIL.PushTypeToStack(methodToProxy.ReturnType, getTypeFromHandle);
                    methodIL.Emit(OpCodes.Stloc_2);

                    //check if this is a generic interface method
                    if (methodToProxy.ContainsGenericParameters)
                    {
                        //get generic parameters
                        Type[] genericParameters = methodToProxy.GetGenericArguments();
                        dynamicMethod.DefineGenericParameters(genericParameters.Select(g => g.Name).ToArray());

                        //build array of generic types
                        methodIL.Emit(OpCodes.Ldc_I4, genericParameters.Length);
                        methodIL.Emit(OpCodes.Newarr, typeType);
                        for (int n = 0; n < genericParameters.Length; n++)
                        {
                            //add each type to the array
                            methodIL.Emit(OpCodes.Dup);
                            methodIL.Emit(OpCodes.Ldc_I4, n);
                            methodIL.PushTypeToStack(genericParameters[n], getTypeFromHandle);
                            methodIL.Emit(OpCodes.Stelem_Ref);
                        }
                    }
                    else
                    {
                        //empty generic type array
                        methodIL.Emit(OpCodes.Ldc_I4, 0);
                        methodIL.Emit(OpCodes.Newarr, typeType);
                    }

                    //store generic type array at variable index 3
                    methodIL.Emit(OpCodes.Stloc_3);

                    //build array of argument names
                    methodIL.Emit(OpCodes.Ldc_I4, parameters.Length);
                    methodIL.Emit(OpCodes.Newarr, stringType);
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        //add each name to the array
                        methodIL.Emit(OpCodes.Dup);
                        methodIL.Emit(OpCodes.Ldc_I4, n);
                        methodIL.Emit(OpCodes.Ldstr, parameters[n].Name);
                        methodIL.Emit(OpCodes.Stelem_Ref);
                    }

                    //store argument name array at variable index 4
                    methodIL.Emit(OpCodes.Stloc_S, 4);

                    //build array of argument types
                    methodIL.Emit(OpCodes.Ldc_I4, parameters.Length);
                    methodIL.Emit(OpCodes.Newarr, typeType);
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        //add each type to the array
                        methodIL.Emit(OpCodes.Dup);
                        methodIL.Emit(OpCodes.Ldc_I4, n);
                        methodIL.PushTypeToStack(parameters[n].ParameterType, getTypeFromHandle);
                        methodIL.Emit(OpCodes.Stelem_Ref);
                    }

                    //store argument type array at variable index 5
                    methodIL.Emit(OpCodes.Stloc_S, 5);

                    //build array of argument values
                    methodIL.Emit(OpCodes.Ldc_I4, parameters.Length);
                    methodIL.Emit(OpCodes.Newarr, objectType);
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        //add each value to the array
                        methodIL.Emit(OpCodes.Dup);
                        methodIL.Emit(OpCodes.Ldc_I4, n);
                        methodIL.Emit(OpCodes.Ldarg, n + 1);
                        methodIL.Emit(OpCodes.Box, parameters[n].ParameterType);
                        methodIL.Emit(OpCodes.Stelem_Ref);
                    }

                    //store argument value array at variable index 6
                    methodIL.Emit(OpCodes.Stloc_S, 6);

                    //pass args to method
                    methodIL.Emit(OpCodes.Ldarg_0);
                    methodIL.Emit(OpCodes.Ldloc_0);
                    methodIL.Emit(OpCodes.Ldloc_1);
                    methodIL.Emit(OpCodes.Ldloc_2);
                    methodIL.Emit(OpCodes.Ldloc_3);
                    methodIL.Emit(OpCodes.Ldloc_S, 4);
                    methodIL.Emit(OpCodes.Ldloc_S, 5);
                    methodIL.Emit(OpCodes.Ldloc_S, 6);

                    //call method and return
                    methodIL.Emit(OpCodes.Call, implementationMethod);
                    methodIL.Emit(OpCodes.Ret);

                    //associate dynamic method with interface method
                    typeBuilder.DefineMethodOverride(dynamicMethod, methodToProxy);
                    if (!this._proxyReturnTypes[interfaceType.AssemblyQualifiedName].ContainsKey(methodToProxy.Name))
                        this._proxyReturnTypes[interfaceType.AssemblyQualifiedName].Add(methodToProxy.Name, methodToProxy.ReturnType);
                }

                //get all events defined on the interface
                foreach (EventInfo eventToProxy in interfaceType.GetEvents())
                {
                    //create event
                    string eventName = eventToProxy.Name;
                    Type eventType = eventToProxy.EventHandlerType;
                    EventBuilder eventBuilder = typeBuilder.DefineEvent(eventName, EventAttributes.None, eventType);
                    string addMethodName = $"{WebWorkerConstants.Proxy.DynamicEventAddMethod}_{eventName}";
                    FieldBuilder eventFieldBuilder = typeBuilder.DefineField(eventName, eventType, FieldAttributes.Public);
                    string removeMethodName = $"{WebWorkerConstants.Proxy.DynamicEventRemoveMethod}_{eventName}";

                    //build pass-through/stubbed add method
                    MethodBuilder eventAddMethodBuilder = typeBuilder.DefineMethod(addMethodName, eventMethodAttributes, null, new Type[] { eventType });
                    eventAddMethodBuilder.SetImplementationFlags(eventMethodImplementationAttributes);
                    ILGenerator addMethodIL = eventAddMethodBuilder.GetILGenerator();

                    //implement add method
                    addMethodIL.Emit(OpCodes.Ldarg_0);
                    addMethodIL.Emit(OpCodes.Ldarg_0);
                    addMethodIL.Emit(OpCodes.Ldfld, eventFieldBuilder);
                    addMethodIL.Emit(OpCodes.Ldarg_1);
                    addMethodIL.Emit(OpCodes.Call, eventAddMethod);
                    addMethodIL.Emit(OpCodes.Castclass, eventType);
                    addMethodIL.Emit(OpCodes.Stfld, eventFieldBuilder);
                    addMethodIL.Emit(OpCodes.Ret);

                    //build pass-through/stubbed remove method
                    MethodBuilder eventRemoveMethodBuilder = typeBuilder.DefineMethod(removeMethodName, eventMethodAttributes, null, new Type[] { eventType });
                    eventRemoveMethodBuilder.SetImplementationFlags(eventMethodImplementationAttributes);
                    ILGenerator removeMethodIL = eventRemoveMethodBuilder.GetILGenerator();

                    //implement remove method
                    removeMethodIL.Emit(OpCodes.Ldarg_0);
                    removeMethodIL.Emit(OpCodes.Ldarg_0);
                    removeMethodIL.Emit(OpCodes.Ldfld, eventFieldBuilder);
                    removeMethodIL.Emit(OpCodes.Ldarg_1);
                    removeMethodIL.Emit(OpCodes.Call, eventRemoveMethod);
                    removeMethodIL.Emit(OpCodes.Castclass, eventType);
                    removeMethodIL.Emit(OpCodes.Stfld, eventFieldBuilder);
                    removeMethodIL.Emit(OpCodes.Ret);

                    //associate dynamic event with interface event
                    eventBuilder.SetAddOnMethod(eventAddMethodBuilder);
                    eventBuilder.SetRemoveOnMethod(eventRemoveMethodBuilder);
                    typeBuilder.DefineMethodOverride(eventAddMethodBuilder, interfaceType.GetMethod($"{WebWorkerConstants.Proxy.InterfaceEventAddMethod}{eventName}"));
                    typeBuilder.DefineMethodOverride(eventRemoveMethodBuilder, interfaceType.GetMethod($"{WebWorkerConstants.Proxy.InterfaceEventRemoveMethod}{eventName}"));

                    //store type
                    if (!this._proxyReturnTypes[interfaceType.AssemblyQualifiedName].ContainsKey(eventName))
                        this._proxyReturnTypes[interfaceType.AssemblyQualifiedName].Add(eventName, eventType);
                }

                //get all properties defined on the interface
                foreach (PropertyInfo propertyToProxy in interfaceType.GetProperties())
                {
                    //define property
                    string propertyName = propertyToProxy.Name;
                    string propertyGetterName = $"{WebWorkerConstants.Proxy.PropertyGetMethod}{propertyName}";
                    string propertySetterName = $"{WebWorkerConstants.Proxy.PropertySetMethod}{propertyName}";
                    PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyToProxy.PropertyType, null);
                    FieldBuilder propertyFieldBuilder = typeBuilder.DefineField($"{WebWorkerConstants.Proxy.InternalField}{propertyName}", typeof(string), FieldAttributes.Private);

                    //check getter
                    MethodInfo propertyGetterMethod = interfaceType.GetMethod(propertyGetterName);
                    if (propertyGetterMethod != null)
                    {
                        //build pass-through/stubbed getter method
                        MethodBuilder propertyGetterMethodBuilder = typeBuilder.DefineMethod(propertyGetterName, propertyAttributes, propertyToProxy.PropertyType, Type.EmptyTypes);
                        ILGenerator propertyGetterIL = propertyGetterMethodBuilder.GetILGenerator();

                        //implement getter method
                        propertyGetterIL.Emit(OpCodes.Ldarg_0);
                        propertyGetterIL.Emit(OpCodes.Ldfld, propertyFieldBuilder);
                        propertyGetterIL.Emit(OpCodes.Ret);

                        //associate dynamic property getter with interface property getter
                        propertyBuilder.SetGetMethod(propertyGetterMethodBuilder);
                        typeBuilder.DefineMethodOverride(propertyGetterMethodBuilder, propertyGetterMethodBuilder);
                    }

                    //check setter
                    MethodInfo propertySetterMethod = interfaceType.GetMethod(propertySetterName);
                    if (propertySetterMethod != null)
                    {
                        //build pass-through/stubbed setter method
                        MethodBuilder propertySetterMethodBuilder = typeBuilder.DefineMethod(propertySetterName, propertyAttributes, null, new Type[] { propertyToProxy.PropertyType });
                        ILGenerator custNameSetIL = propertySetterMethodBuilder.GetILGenerator();

                        //implement setter method
                        custNameSetIL.Emit(OpCodes.Ldarg_0);
                        custNameSetIL.Emit(OpCodes.Ldarg_1);
                        custNameSetIL.Emit(OpCodes.Stfld, propertyFieldBuilder);
                        custNameSetIL.Emit(OpCodes.Ret);

                        //associate dynamic property setter with interface property setter
                        propertyBuilder.SetSetMethod(propertySetterMethodBuilder);
                        typeBuilder.DefineMethodOverride(propertySetterMethodBuilder, propertySetterMethodBuilder);
                    }
                }

                //return
                return typeBuilder.CreateType();
            });

            //return
            return (I)Activator.CreateInstance(proxyType, module, webWorkerId, fileUploadControlId);
        }

        /// <summary>
        /// Registers callbacks on an proxied interface method when the method is void.
        /// </summary>
        public Guid RegisterVoidMethodInvocationCallbacks<I>(I proxy, string methodName, Func<Guid, Task> successCallback = null, Func<ErrorMessageModel, Task> errorCallback = null)
        {
            //initialization
            Func<object, Guid, Task> voidSuccessCallback = successCallback == null ? null : async (_, invocationId) => await successCallback(invocationId);

            //return
            return this.RegisterMethodInvocationCallbacks<I, object>(proxy, methodName, voidSuccessCallback, errorCallback);
        }

        /// <summary>
        /// Registers callbacks on an proxied interface method.
        /// </summary>
        public Guid RegisterMethodInvocationCallbacks<I, R>(I proxy, string methodName, Func<R, Guid, Task> successCallback, Func<ErrorMessageModel, Task> errorCallback = null)
        {
            //initialization           
            Type returnType = typeof(R);
            Type interfaceType = typeof(I);
            Guid invocationId = Guid.NewGuid();
            string message = $" {interfaceType.Name}.{methodName}";
            string interfaceTypeKey = interfaceType.AssemblyQualifiedName;

            //ensure the requested method has been proxied for the given interface type
            if (!this._proxyReturnTypes.ContainsKey(interfaceTypeKey))
                throw new ArgumentOutOfRangeException($"Interface {interfaceType.Name} has not been proxied.");
            if (!this._proxyReturnTypes[interfaceTypeKey].ContainsKey(methodName))
                throw new ArgumentOutOfRangeException($"Method {methodName} on interface {interfaceType.Name} has not been proxied.");

            //check result type
            Type proxyReturnType = this._proxyReturnTypes[interfaceTypeKey][methodName];
            if (proxyReturnType != typeof(void) && proxyReturnType != returnType)
            {
                //check task
                if (proxyReturnType == typeof(Task) && !proxyReturnType.IsGenericType)
                {
                    //treat non-generic tasks as voids
                    this._logger.LogDebug($"Not checking non-generic task return type for{message}.");
                }
                else
                {
                    //check task result type
                    Type taskType = typeof(Task<>).MakeGenericType(returnType);
                    if (proxyReturnType != taskType)
                        throw new InvalidOperationException($"{returnType.FullName} is not the correct return type for method {methodName} on interface {interfaceType.Name}; {proxyReturnType.FullName} was expected.");
                }
            }

            //check success callback
            if (successCallback != null)
            {
                //ensure success callback
                if (!this._proxySuccessCallbacks.ContainsKey(interfaceTypeKey))
                    this._proxySuccessCallbacks.Add(interfaceTypeKey, new Dictionary<string, object>());
                if (!this._proxySuccessCallbacks[interfaceTypeKey].ContainsKey(methodName))
                    this._proxySuccessCallbacks[interfaceTypeKey].Add(methodName, successCallback);
                else
                    this._proxySuccessCallbacks[interfaceTypeKey][methodName] = successCallback;
            }

            //check error callback
            if (errorCallback != null)
            {
                //ensure error callback
                if (!this._proxyErrorCallbacks.ContainsKey(interfaceTypeKey))
                    this._proxyErrorCallbacks.Add(interfaceTypeKey, new Dictionary<string, Func<ErrorMessageModel, Task>>());
                if (!this._proxyErrorCallbacks[interfaceTypeKey].ContainsKey(methodName))
                    this._proxyErrorCallbacks[interfaceTypeKey].Add(methodName, errorCallback);
                else
                    this._proxyErrorCallbacks[interfaceTypeKey][methodName] = errorCallback;
            }

            //return
            this.GetPrivateField(proxy.GetType(), WebWorkerConstants.Proxy.InvocationIdFieldName).SetValue(proxy, invocationId);
            this._logger.LogInformation($"Successfully registered callbacks on{message}.");
            return invocationId;
        }

        /// <summary>
        /// Registers an event callback for a single method invocation.
        /// </summary>
        public void RegisterVoidEventCallback<I>(I proxy, Guid invocationId, string eventName, Func<Guid, Task> eventCallback)
        {
            //initialization
            Func<object, Guid, Task> voidSuccessCallback = async (_, invocationId) => await eventCallback(invocationId);

            //return
            this.RegisterEventCallback<I, object>(proxy, invocationId, eventName, voidSuccessCallback);
        }

        /// <summary>
        /// Registers a strongly-typed event callback for a single method invocation.
        /// </summary>
        public void RegisterEventCallback<I, E>(I proxy, Guid invocationId, string eventName, Func<E, Guid, Task> eventCallback)
        {
            //initialization
            Type interfaceType = typeof(I);
            Type proxyType = proxy.GetType();
            string message = $" {interfaceType.Name}.{eventName}";
            string eventReturnTypeName = typeof(E).AssemblyQualifiedName;
            string interfaceTypeKey = interfaceType.AssemblyQualifiedName;

            //check event callback
            if (eventCallback != null)
            {
                //ensure the requested event has been proxied for the given interface type
                if (!this._proxyReturnTypes.ContainsKey(interfaceTypeKey))
                    throw new ArgumentOutOfRangeException($"Interface {interfaceType.Name} has not been proxied.");
                if (!this._proxyReturnTypes[interfaceTypeKey].ContainsKey(eventName))
                    throw new ArgumentOutOfRangeException($"Event {eventName} on interface {interfaceType.Name} has not been proxied.");

                //check invocation
                Guid proxyInvocationId = (Guid)this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.InvocationIdFieldName).GetValue(proxy);
                if (proxyInvocationId != invocationId)
                    throw new InvalidOperationException($"Method invocation {proxyInvocationId} on {interfaceType.Name} doesn't match {invocationId} so a callback for event {eventName} could not be registered.");

                //ensure event callback
                if (!this._proxyEventCallbacks.ContainsKey(proxyInvocationId))
                    this._proxyEventCallbacks.Add(proxyInvocationId, new Dictionary<string, object>());
                if (!this._proxyEventCallbacks[proxyInvocationId].ContainsKey(eventName))
                    this._proxyEventCallbacks[proxyInvocationId].Add(eventName, eventCallback);
                else
                    this._proxyEventCallbacks[proxyInvocationId][eventName] = eventCallback;

                //get proxy registration
                FieldInfo eventField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.EventRegistrationsFieldName);
                Dictionary<string, string> proxyCallbacks = (Dictionary<string, string>)eventField.GetValue(proxy);
                proxyCallbacks ??= new Dictionary<string, string>();

                //ensure proxy registration
                if (!proxyCallbacks.ContainsKey(eventName))
                    proxyCallbacks.Add(eventName, eventReturnTypeName);
                else
                    proxyCallbacks[eventName] = eventReturnTypeName;

                //update proxy registration
                eventField.SetValue(proxy, proxyCallbacks);
            }
            else
            {
                //error
                throw new ArgumentNullException($"The callback for event{message} was not provided.");
            }

            //return
            this._logger.LogInformation($"Successfully registered event callback for{message}.");
        }

        /// <summary>
        /// Fires the callback when a worker is created.
        /// </summary>
        [JSInvokable()]
        public async Task WorkerCreatedAsync(Guid workerId)
        {
            //initialization
            this._webWorkerIds.Add(workerId);
            List<Task> tasks = new List<Task>();
            this._logger.LogDebug($"Created and web worker {workerId}.");

            //check worker callbacks
            if (!this._creationCallbacks.ContainsKey(workerId))
                throw new Exception($"No callbacks have been registered for worker {workerId}.");

            //fire callbacks
            foreach (Func<Guid, Task> callback in this._creationCallbacks[workerId].Where(c => c != null))
                tasks.Add(callback.Invoke(workerId));

            //wait for work to finish
            AggregateException error = await WebWorkerUtilities.WhenAllAsync(tasks);
            if (error != null)
                throw error;

            //return
            this._creationCallbacks.Remove(workerId);
        }

        /// <summary>
        /// Fires the callback when a worker has completed.
        /// </summary>
        [JSInvokable()]
        public async Task WebWorkerFinishedAsync(ResultMessageModel model)
        {
            //initialization
            object callback = this.GetProxyCallback<object>(model, this._proxySuccessCallbacks, "success");

            //return
            ErrorMessageModel result = await this.InvokeGenericCallbackAsync(callback, model.Proxy.ReturnTypeName, model.Result, model);
            if (result == null)
                this._logger.LogInformation($"Successfully invoked success callback for {model}.");
            else
                await this.WebWorkerFailedAsync(result);
        }

        /// <summary>
        /// Fires the callback when a worker has reported progress.
        /// </summary>
        [JSInvokable()]
        public async Task WebWorkerEventRaisedAsync(EventHandlerMessageModel model)
        {
            //initialization
            this._logger.LogTrace($"Handling event {model}.");

            //first check for strongly-typed callbacks
            if (this._proxyEventCallbacks.ContainsKey(model.InvocationId) && this._proxyEventCallbacks[model.InvocationId].ContainsKey(model.EventName))
            {
                //get callback
                object callback = this._proxyEventCallbacks[model.InvocationId][model.EventName];

                //return
                ErrorMessageModel result = await this.InvokeGenericCallbackAsync(callback, model.EventArgumentTypeName, model.Result, model);
                if (result == null)
                    this._logger.LogDebug($"Successfully invoked direct event callback for {model}.");
                else
                    await this.WebWorkerFailedAsync(result);
            }
            else
            {
                //error
                this._logger.LogWarning($"Could not find a callback for event {model.EventName} for invocation {model.InvocationId}.");
            }
        }

        /// <summary>
        /// Fires the callback when a worker has Failed.
        /// </summary>
        [JSInvokable()]
        public async Task WebWorkerFailedAsync(ErrorMessageModel model)
        {
            //initialization
            if (model.Proxy == null)
            {
                //this indicates a non-invocation error; the invocation ID will be the web worker ID in these cases
                throw new Exception($"Web Worker {model.InvocationId} has reported the following error: {model.Error}");
            }

            //get callback
            Func<ErrorMessageModel, Task> callback = this.GetProxyCallback<Func<ErrorMessageModel, Task>>(model, this._proxyErrorCallbacks, "error");
            if (callback == null)
                return;

            //return
            await callback.Invoke(model);
            this._logger.LogInformation($"Successfully invoked error callback for {model}.");
        }

        /// <summary>
        /// Gets an Azure B2C token from browser session storage.
        /// </summary>
        [JSInvokable()]
        public async Task<AzureB2CTokenModel> GetB2CTokenAsync()
        {
            //initialization
            AuthenticationState state = await this._authenticationStateProvider.GetAuthenticationStateAsync();
            if (!state?.User?.Identity?.IsAuthenticated ?? true)
            {
                //anonymous
                return null;
            }

            //get b2c settings
            Guid appId = this._settingsService.GetSetting<Guid>(WebWorkerConstants.Security.Settings.AppId);
            Guid tenantId = this._settingsService.GetSetting<Guid>(WebWorkerConstants.Security.Settings.TenantId);
            string policy = this._settingsService.GetSetting<string>(WebWorkerConstants.Security.Settings.Policy);
            string scope = this._settingsService.GetSetting<string>(WebWorkerConstants.Security.Settings.AccessScope);
            string instance = this._settingsService.GetSetting<string>(WebWorkerConstants.Security.Settings.Instance);
            Guid currentUserId = Guid.Parse(state.User.GetClaimValueWithFallback(WebWorkerConstants.Security.Claims.OID, WebWorkerConstants.Security.Claims.ID));

            //get token
            string key = WebWorkerUtilities.BuildAzureB2CTokenSessionKey(currentUserId, policy, tenantId, instance, appId, scope);
            AzureB2CTokenModel token = await this._sessionStorageService.GetItemAsync<AzureB2CTokenModel>(key);

            //log
            string message = $" Azure B2C token at {key}.";
            if (token != null)
                this._logger.LogInformation($"Found{message}");
            else
                this._logger.LogError($"Could not find{message}");

            //return
            return token;
        }
        
        /// <summary>
        /// This is the method invoked to proxy sync void calls.
        /// </summary>
        [Obsolete(WebWorkerConstants.Messages.ProxyOnly)]
        public void ProxyMethodVoidSync(Type interfaceType, string methodName, Type returnType, Type[] genericTypes, string[] argNames, Type[] argTypes, object[] argValues)
        {
            //return
            this.ProxyMethodReturnSync<object>(interfaceType, methodName, returnType, genericTypes, argNames, argTypes, argValues);
        }

        /// <summary>
        /// This is the method invoked to proxy sync return calls.
        /// </summary>
        [Obsolete(WebWorkerConstants.Messages.ProxyOnly)]
        public T ProxyMethodReturnSync<T>(Type interfaceType, string methodName, Type returnType, Type[] genericTypes, string[] argNames, Type[] argTypes, object[] argValues)
        {
            //initialization
            ProxyModel model = this.GetProxyModel(interfaceType, methodName, returnType, genericTypes, argNames, argTypes, argValues);

            //return
            WebWorkerUtilities.FireAndForget(async () => await this.InvokeProxyMethodAsync(model), this._logger);
            return default(T);
        }

        /// <summary>
        /// This is the method invoked to proxy async void calls.
        /// </summary>
        [Obsolete(WebWorkerConstants.Messages.ProxyOnly)]
        public async Task ProxyMethodVoidAsync(Type interfaceType, string methodName, Type returnType, Type[] genericTypes, string[] argNames, Type[] argTypes, object[] argValues)
        {
            //return
            await this.ProxyMethodReturnAsync<object>(interfaceType, methodName, returnType, genericTypes, argNames, argTypes, argValues);
        }

        /// <summary>
        /// This is the method invoked to proxy async return calls.
        /// </summary>
        [Obsolete(WebWorkerConstants.Messages.ProxyOnly)]
        public async Task<T> ProxyMethodReturnAsync<T>(Type interfaceType, string methodName, Type returnType, Type[] genericTypes, string[] argNames, Type[] argTypes, object[] argValues)
        {
            //initialization
            ProxyModel model = this.GetProxyModel(interfaceType, methodName, returnType, genericTypes, argNames, argTypes, argValues);

            //return
            await this.InvokeProxyMethodAsync(model);
            return default(T);
        }
        #endregion           
        #region Private Methods       
        /// <summary>
        /// Builds a proxy model from IL-generated methods.
        /// </summary>
        private ProxyModel GetProxyModel(Type interfaceType, string methodName, Type returnType, Type[] genericTypes, string[] argNames, Type[] argTypes, object[] argValues)
        {
            //initialization
            int[] lengths = new int[] { argNames?.Length ?? 0, argTypes?.Length ?? 0, argValues?.Length ?? 0 };
            if (!lengths.All(l => l.Equals(lengths.First())))
                throw new ArgumentException($"For method proxy {methodName}, the argument names, types, and values are asymetric.");

            //build model
            ProxyModel model = new ProxyModel()
            {
                //assemble object
                MethodName = methodName,
                ReturnTypeName = returnType.AssemblyQualifiedName,
                InterfaceType = interfaceType.AssemblyQualifiedName,
                GenericTypeNames = genericTypes.Select(t => t.AssemblyQualifiedName).ToArray()
            };

            //add parameters
            for (int n = 0; n < argNames.Length; n++)
                model.AddParameter(argNames[n], argValues[n], argTypes[n].AssemblyQualifiedName);

            //return
            return model;
        }

        /// <summary>
        /// Marshals a proxy method to a web worker. NOTE: "this" here will be the *proxied* type, not the concrete type, so we need reflection to access its instance variables.
        /// </summary>
        private async Task InvokeProxyMethodAsync(ProxyModel model)
        {
            //initialization
            Type proxyType = this.GetType();
            FieldInfo webWorkerIdField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.WebWorkerIdFieldName);
            FieldInfo jsModuleField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.JavaScriptModuleFieldName);
            FieldInfo invocationIdField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.InvocationIdFieldName);
            FieldInfo fileControlIdField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.FileControlIdFieldName);
            FieldInfo eventRegistrationsField = this.GetPrivateField(proxyType, WebWorkerConstants.Proxy.EventRegistrationsFieldName);

            //get field values
            Guid webWorkerId = (Guid)webWorkerIdField.GetValue(this);
            Guid invocationId = (Guid)invocationIdField.GetValue(this);
            string fileUploadControlId = (string)fileControlIdField.GetValue(this);
            IJSObjectReference module = (IJSObjectReference)jsModuleField.GetValue(this);
            Dictionary<string, string> eventRegistrations = (Dictionary<string, string>)eventRegistrationsField.GetValue(this);

            //return
            await module.InvokeVoidAsync(WebWorkerConstants.JavaScriptInterop.Functions.InvokeWebWorker, webWorkerId, invocationId, model, eventRegistrations, fileUploadControlId);
        }

        /// <summary>
        /// Extracts and invokes a proxy callback.
        /// </summary>
        private T GetProxyCallback<T>(BaseMessageModel model, Dictionary<string, Dictionary<string, T>> callbacks, string callbackType)
        {
            //initialization
            if (model == null)
            {
                //error
                this._logger.LogError("A blank web worker result message was received.");
                return default(T);
            }
            else
            {
                //log
                this._logger.LogInformation($"Handling web worker result {model}.");
            }

            //unpack callback
            string methodNameKey = model.Proxy.MethodName;
            string interfaceTypeKey = model.Proxy.InterfaceType;
            string message = $" for web worker invocation {model.InvocationId}";
            this._logger.LogInformation($"Getting {callbackType} callback{message}.");

            //check callback
            if (!callbacks.ContainsKey(interfaceTypeKey))
            {
                //no callback on interface
                this._logger.LogInformation($"No {callbackType} callbacks are registered on {interfaceTypeKey}{message}.");
                return default(T);
            }
            else if (!callbacks[interfaceTypeKey].ContainsKey(methodNameKey))
            {
                //no callback on method
                this._logger.LogInformation($"No {callbackType} callbacks are registered for {interfaceTypeKey}.{methodNameKey}{message}.");
                return default(T);
            }

            //get callback
            T callback = callbacks[interfaceTypeKey][methodNameKey];
            if (callback == null)
            {
                //no callback on method
                this._logger.LogWarning($"The {callbackType} callback for {interfaceTypeKey}.{methodNameKey}{message} is null.");
                return default(T);
            }

            //return
            this._logger.LogInformation($"Got {callbackType} callback{message}.");
            return callback;
        }

        /// <summary>
        /// Gets a reference to a private field on a type.
        /// </summary>
        private FieldInfo GetPrivateField(Type type, string fieldName)
        {
            //return
            return type.GetField(fieldName, BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>
        /// Handles deserialization of JavaScript results that could be null or async.
        /// </summary>
        private object DeserializeValue(object result, Type type)
        {
            //initialization
            this._logger.LogTrace($"Deserializing {result?.ToString() ?? "N/A"} of type {type.AssemblyQualifiedName}.");

            //get result value
            if (result != null)
            {
                //get result type
                if (type.IsAsync())
                {
                    //treat non-generic async results as voids
                    Type[] genericArguments = type.GetGenericArguments();
                    if (genericArguments.Any())
                        type = type.GetGenericArguments().Single();
                    else
                        return null;
                }

                //deserialize result, which the JS runtime presents as a JsonElement
                return ((JsonElement)result).Deserialize(type, WebWorkerUtilities.BuildSerializerOptions(false));
            }

            //return
            return null;
        }

        /// <summary>
        /// Deserializes a raw results and invokes a generic callback for it.
        /// </summary>
        private async Task<ErrorMessageModel> InvokeGenericCallbackAsync(object callback, string returnTypeName, JsonElement rawResult, BaseMessageModel model)
        {
            //initialization
            string message = $" callback for {model}";
            if (callback == null)
                return null;
            else
                this._logger.LogDebug($"Invoking{message}.");

            //get result type
            Type resultType = this._typeCache.GetOrAdd(returnTypeName, _ =>
            {
                //return
                return Type.GetType(returnTypeName);
            });

            //get callback method
            MethodInfo method = this._handlerCache.GetOrAdd(callback.GetType().AssemblyQualifiedName, _ =>
            {
                //return
                return callback.GetType().GetMethod(nameof(MethodInfo.Invoke));
            });

            //deserialize result
            object result = null;
            if (resultType != typeof(void))
            {
                //handle primitive types
                if (WebWorkerUtilities.IsPrimitive(resultType))
                    result = rawResult.ToString().ConvertFromString(resultType);
                else
                    result = this.DeserializeValue(rawResult, resultType);
            }

            //this converts and exception to an error response
            ErrorMessageModel processError(Exception exception)
            {
                //initialization
                string error = exception?.ToString() ?? "Unknown Error";
                this._logger.LogCritical($"Failed to execute{message}: {error}");

                //return
                return new ErrorMessageModel(model.InvocationId, model.Proxy, error);
            }

            try
            {
                //execute task
                Task task = (Task)method.Invoke(callback, new object[] { result, model.InvocationId });
                await task.ConfigureAwait(false);

                //return
                if (task.Exception != null)
                {
                    //exception
                    return processError(task.Exception);
                }
                else if (task.IsFaulted)
                {
                    //unknown error
                    return processError(null);
                }
                else
                {
                    //success
                    this._logger.LogDebug($"Successfully exectued{message}.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                //error
                return processError(ex);
            }
        }
        #endregion
    }
}
