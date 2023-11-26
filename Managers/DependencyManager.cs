using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using LinesOfCode.Web.Workers.Models;
using LinesOfCode.Web.Workers.Contracts;
using LinesOfCode.Web.Workers.Utilities;

using Blazored.SessionStorage;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This configures dependency injection and job harnessing.
    /// </summary>
    public static class DependencyManager
    {
        #region Public Methods
        /// <summary>
        /// Registers web worker services.
        /// </summary>
        public static void AddWebWorkers(this WebAssemblyHostBuilder builder, Action<WebWorkerSettings> options = null)
        {
            //initialization
            if (options != null)
            {
                //acquire options
                Dictionary<string, string> allSettings = new Dictionary<string, string>();
                WebWorkerSettings settings = new WebWorkerSettings();
                options.Invoke(settings);

                //check azure b2c settings
                if (settings.AzureB2CSettings != null)
                {
                    //add azure b2c settings
                    allSettings.Add(WebWorkerConstants.Security.Settings.AppId, settings.AzureB2CSettings.AppId);
                    allSettings.Add(WebWorkerConstants.Security.Settings.Policy, settings.AzureB2CSettings.Policy);
                    allSettings.Add(WebWorkerConstants.Security.Settings.TenantId, settings.AzureB2CSettings.TenantId);
                    allSettings.Add(WebWorkerConstants.Security.Settings.Instance, settings.AzureB2CSettings.Instance);
                    allSettings.Add(WebWorkerConstants.Security.Settings.AccessScope, settings.AzureB2CSettings.AccessScope);
                }

                //add settings
                builder.Configuration.AddInMemoryCollection(allSettings);
            }

            //return
            builder.Services.AddScoped<IWebWorkerManager, WebWorkerManager>();
            builder.RegisterServices(new SettingsManager(builder.Configuration.Build()));
        }

        /// <summary>
        /// Configures a Blazor environment for web workers.
        /// </summary>
        public static async Task UseWebWorkersAsync(this WebAssemblyHostBuilder builder, Action<IServiceCollection, ISettingsManager> addWebWorkerDependencies = null)
        {
            //initialization
            IJSInProcessRuntime jsRuntime = DependencyManager.GetJSRuntime();

            //load the web worker proxy manager
            builder.RootComponents.Add<ProxyManager>(WebWorkerConstants.Hosting.Body);
            builder.Services.AddHTTPClient(builder.HostEnvironment.BaseAddress, WebWorkerConstants.Hosting.APIAnonymous);

            //get settings from javascript
            AzureB2CTokenModel token = await jsRuntime.InvokeAsync<AzureB2CTokenModel>(WebWorkerConstants.JavaScript.GetWebWorkerToken);
            Dictionary<string, string> settings = await jsRuntime.InvokeAsync<Dictionary<string, string>>(WebWorkerConstants.JavaScript.GetWebWorkerSettings);

            //configure settings
            builder.Services.AddOptions();
            builder.Configuration.AddEnvironmentVariables();
            builder.Configuration.Add(new MemoryConfigurationSource { InitialData = settings });
            SettingsManager settingsManager = new SettingsManager(builder.Configuration.Build(), token, true);

            //add authenticated http client
            builder.Services.AddHttpClient(WebWorkerConstants.Hosting.APIAuthorized, (container, client) =>
            {
                //check token
                if (token == null)
                {
                    //get token
                    ISettingsManager httpClientSettingsManager = container.GetRequiredService<ISettingsManager>();
                    token = httpClientSettingsManager.Token;
                }

                //check token
                if (token == null)
                    throw new InvalidOperationException("Cannot create an authenticated http cilent because a token was not found.");

                //use the auth token sent over from the main UI thread
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Secret);
            });

            //return
            builder.RegisterServices(settingsManager);
            addWebWorkerDependencies?.Invoke(builder.Services, settingsManager);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Registers web worker services and resources.
        /// </summary>
        private static void RegisterServices(this WebAssemblyHostBuilder builder, ISettingsManager settingsManager = null)
        {
            //initialization
            builder.Services.AddSingleton<ISerializationManager, SerializationManager>();
            builder.Services.AddSingleton(typeof(IMemoryCacheManager<,>), typeof(MemoryCacheManager<,>));
            builder.Services.AddSingleton<ISettingsManager>(settingsManager ?? new SettingsManager(builder.Configuration));

            //return
            builder.Services.AddBlazoredSessionStorage(options => options.JsonSerializerOptions.ApplyJSONConfiguration(true));
        }

        /// <summary>
        /// Uses reflection to construct an in-process JS runtime.
        /// </summary>
        private static IJSInProcessRuntime GetJSRuntime()
        {
            //initialization
            Type jsRuntimeType = typeof(WebAssemblyHost).Assembly.GetType(WebWorkerConstants.Hosting.JSRuntime.TypeName);
            FieldInfo instanceField = jsRuntimeType.GetField(WebWorkerConstants.Hosting.JSRuntime.Instance, BindingFlags.NonPublic | BindingFlags.Static);

            //return
            return (IJSInProcessRuntime)instanceField.GetValue(null);
        }

        /// <summary>
        /// Configures HTTP clients.
        /// </summary>
        private static IHttpClientBuilder AddHTTPClient(this IServiceCollection services, string url, string name)
        {
            //return
            return services.AddHttpClient(name, (client) =>
            {
                //conifgure anonymous calls to the server
                client.BaseAddress = new Uri(url);
            });
        }
        #endregion
    }
}
