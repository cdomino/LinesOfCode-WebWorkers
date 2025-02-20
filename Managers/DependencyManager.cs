using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

using Microsoft.JSInterop;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using LinesOfCode.Web.Workers.Mock;
using LinesOfCode.Web.Workers.Models;
using LinesOfCode.Web.Workers.Services;
using LinesOfCode.Web.Workers.Utilities;
using LinesOfCode.Web.Workers.Contracts.Services;
using LinesOfCode.Web.Workers.Contracts.Managers;

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
        /// Registers web worker services for Blazor Web Apps.
        /// </summary>
        public static void AddWebWorkers(this IHostApplicationBuilder builder, Action<WebWorkerSettingsModel> options = null)
        {
            //initialization
            builder.Services.AddWebWorkers(builder.Configuration, (IConfigurationRoot)builder.Configuration, options);
        }

        /// <summary>
        /// Registers web worker services for Blazor Web Apps.
        /// </summary>
        public static void AddWebWorkers(this IHostApplicationBuilder builder)
        {
            //initialization
            builder.Services.AddWebWorkers(builder.Configuration, (IConfigurationRoot)builder.Configuration, null);
        }

        /// <summary>
        /// Registers web worker services for Blazor WASM.
        /// </summary>
        public static void AddWebWorkers(this WebAssemblyHostBuilder builder, Action<WebWorkerSettingsModel> options = null)
        {
            //return
            builder.Services.AddWebWorkers(builder.Configuration, builder.Configuration, options);
        }

        /// <summary>
        /// Registers web worker services for Blazor WASM.
        /// </summary>
        public static void AddWebWorkers(this WebAssemblyHostBuilder builder)
        {
            //return
            builder.Services.AddWebWorkers(builder.Configuration, builder.Configuration, null);
        }

        /// <summary>
        /// Configures a Blazor environment for web workers (WASM only).
        /// </summary>
        public static async Task UseWebWorkersAsync(this WebAssemblyHostBuilder builder, Action<IServiceCollection, ISettingsService> addWebWorkerDependencies = null)
        {
            //initialization
            IJSInProcessRuntime jsRuntime = DependencyManager.GetJSRuntime();

            //load the web worker proxy manager
            builder.RootComponents.Add<ProxyManager>(WebWorkerConstants.Hosting.Body);
            builder.Services.AddHTTPClient(builder.HostEnvironment.BaseAddress, WebWorkerConstants.Hosting.APIAnonymous);

            //get settings from javascript
            AzureB2CTokenModel token = await jsRuntime.InvokeAsync<AzureB2CTokenModel>(WebWorkerConstants.JavaScriptInterop.Functions.GetWebWorkerToken);
            Dictionary<string, string> settings = await jsRuntime.InvokeAsync<Dictionary<string, string>>(WebWorkerConstants.JavaScriptInterop.Functions.GetWebWorkerSettings);

            //configure settings
            builder.Services.AddOptions();
            builder.Configuration.AddEnvironmentVariables();
            builder.Configuration.Add(new MemoryConfigurationSource { InitialData = settings });
            SettingsService settingsService = new SettingsService(builder.Configuration.Build(), token, true);

            //add authenticated http client
            builder.Services.AddHttpClient(WebWorkerConstants.Hosting.APIAuthorized, (container, client) =>
            {
                //check token
                if (token == null)
                {
                    //get token
                    ISettingsService httpClientSettingsService = container.GetRequiredService<ISettingsService>();
                    token = httpClientSettingsService.Token;
                }

                //recheck token
                if (token == null)
                    throw new TokenExpiredException();

                //use the auth token sent over from the main UI thread
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Secret);
            });

            //return
            builder.Services.RegisterServices(builder.Configuration, settingsService);
            addWebWorkerDependencies?.Invoke(builder.Services, settingsService);
        }

        /// <summary>
        /// Uses reflection to construct an in-process JS runtime.
        /// </summary>
        public static IJSInProcessRuntime GetJSRuntime()
        {
            //initialization
            string message = "The default WASM JSRuntime ";
            Type jsRuntimeType = typeof(WebAssemblyHost).Assembly.GetType(WebWorkerConstants.Hosting.JSRuntime.TypeName);
            if (jsRuntimeType == null)
                throw new Exception($"{message} type {WebWorkerConstants.Hosting.JSRuntime.TypeName}could not be found.");

            //get the field the holds the default JS runime
            FieldInfo instanceField = jsRuntimeType.GetField(WebWorkerConstants.Hosting.JSRuntime.Instance, BindingFlags.Public | BindingFlags.Static);
            if (instanceField == null)
                throw new Exception($"{message} field {WebWorkerConstants.Hosting.JSRuntime.Instance}could not be found.");

            //get instance object
            IJSInProcessRuntime jsRuntime = (IJSInProcessRuntime)instanceField.GetValue(null);
            if (jsRuntime == null)
                throw new Exception($"{message}is null.");

            //return
            return jsRuntime;
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Registers web worker services.
        /// </summary>
        private static void AddWebWorkers(this IServiceCollection services, IConfigurationBuilder configurationBuilder, IConfigurationRoot configurationRoot, Action<WebWorkerSettingsModel> options = null)
        {
            //initialization
            if (options != null)
            {
                //acquire options
                Dictionary<string, string> allSettings = new Dictionary<string, string>();
                WebWorkerSettingsModel settings = new WebWorkerSettingsModel();
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

                    //set token timout
                    int tokenRefreshTimeout = settings.AzureB2CSettings.TokenRefreshTimeoutMilliseconds ?? WebWorkerConstants.Security.TokenRefresh.SpinMaxMilliseconds;
                    allSettings.Add(WebWorkerConstants.Security.Settings.TokenRefreshTimeout, Math.Max(tokenRefreshTimeout, WebWorkerConstants.Security.TokenRefresh.SpinWaitMilliseconds).ToString());
                }

                //add settings
                configurationBuilder.AddInMemoryCollection(allSettings);

                //add mock dependencies
                if (settings.UseMockNavigation)
                    services.AddMockNavigation();
                if (settings.UseMockAuthentication)
                    services.AddMockAuthentication();
            }

            //return
            services.AddScoped<IWebWorkerManager, WebWorkerManager>();
            services.RegisterServices(configurationRoot, new SettingsService(configurationRoot));           
        }

        /// <summary>
        /// Registers web worker services and resources.
        /// </summary>
        private static void RegisterServices(this IServiceCollection services, IConfigurationRoot configurationRoot, ISettingsService settingsService = null)
        {
            //initialization
            services.AddSingleton<ISerializationService, SerializationService>();
            services.AddSingleton(typeof(IMemoryCacheManager<,>), typeof(MemoryCacheManager<,>));
            services.AddSingleton<ISettingsService>(settingsService ?? new SettingsService(configurationRoot));

            //return
            services.AddBlazoredSessionStorage(options => options.JsonSerializerOptions.ApplyJSONConfiguration(true));
        }

        /// <summary>
        /// Adds mock authentication and navigation services to satisify authentication dependencies.
        /// </summary>        
        private static void AddMockAuthentication(this IServiceCollection services)
        {
            //initialization
            services.AddScoped<MockAuthenticationStateProvider>();

            //return
            services.AddScoped<AuthenticationStateProvider>(c => c.GetRequiredService<MockAuthenticationStateProvider>());
        }

        /// <summary>
        /// Adds mock authentication and navigation services to satisify authentication dependencies.
        /// </summary>        
        private static void AddMockNavigation(this IServiceCollection services)
        {
            //initialization
            services.AddScoped<MockNavigationManager>();

            //return
            services.AddScoped<NavigationManager>(c => c.GetRequiredService<MockNavigationManager>());
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
