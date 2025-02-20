# Blazor Web Workers

Welcome! Despite the .NET `async` / `await` goodness we've been enjoying in Blazor for years now, JavaScript essentially still only runs on the browser's main UI thread. Web Workers are the shortest path on the way to achieving native parallelism in your client apps as they allow you to spin up and run code on background threads in the browser without blocking UI interaction.

Microsoft kind of flirted with this [as an experiment](https://visualstudiomagazine.com/articles/2022/10/11/blazor-webassembly-net7.aspx) while .NET 8 was still gestating, but it sadly still hasn't made the cut for .NET 9; Web Workers therefore remain absent from Blazor proper, which was the inspiration to publish this package. I have been evolving this code since I first got serious with Blazor WASM back in 2020, and now that I've ported everything to .NET 9 (and confirmed that the new Blazor Web App paradigm didn't completely send me back to the coding board) it's ready to share!

This doc provides a detailed overview of how to use this package to achieve *actual* multithreading in your Blazor web apps. 

## Background 

There are a few decent flavors of this kind of thing out there, but I decided to cook my own after not being able to accept the restrictions inherent to the initial offerings of other intrepid developers in this space. These challenges include requiring copying method parameters to local variables before they could be passed to a worker, not being able to customize the copious serialization going on behind the scenes, not being able to share large/complex objects among the threads, and ultimately having an implementation that just didn't feel like a natural code flow.

I wanted my workers to be more of an implementation detail than a paradigm developers had to adopt. It was important to ~~my OCD~~ me to be able to take any service call and simply run it as a worker rather than having to contort my logic into an awkward posture that was agreeable to the multithreading frameworks out there I was attempting to leverage. There is still of course overhead and churn to anything this complicated, but I think I found the path of least fuckery to get your browser threads up and running quickly.

Here are some other developer quality of life features I am excited to offer:

 - Both async and non-async methods work (async is of course the present and the future, but let's not forget the past; a lot of third party libraries out there seem to have gone full async even when it's not necessary, making legacy sync code awkward to include).
 - VS tools like IntelliSense/Find Reference work as through the code was running on the main thread. Using "real" references in the library (verses `eval` or other approaches that could confuse a tree shaker) is specifically important for Blazor deployment code trimming; you probably don't want to wait 45 minutes for each build to finish before being able to see if your app was mutilated in the process because the trimmer didn't think your service's methods were  in use.
 - Since we can't debug into Blazor Web Worker processes, I include lot of logging to help peek into the black (or at least WASM-colored) box.
 - I tried to keep the .NET Core-ish configuration bits as simple as possible; what you do in Program.cs is your business, not mine.
 - Not only can you call external APIs from worker threads (either to your backend service or a third party), but you can also pass authentication tokens so remote resources can be fetched securely as well.
 - Finally, only performing long running operations that do not block the UI isn't good enough; I provide the ability to report progress via standard .NET events/handlers!

## Installation

The following steps walk you through how to get Blazor Web Workers installed and configured in your app.

 1. Install the [Nuget package](https://www.nuget.org/packages/LinesOfCode.Web.Workers) to your Blazor "client" project. I currently support .NET 8 & 9 (tested in hosted Blazor Web App and WASM), but if there's interest I would be happy to spend more time hardening this for other frameworks/render modes. Since the default Blazor project structure's "server" project references the client one, you shouldn't have to install this anywhere else (unless you need it in your middleware as well).

 2. Crack open your client Program.cs (which is the same logical file for startup code in all recent Blazor versions) and let's get into the tricky bits. Here is a "conventional" view of this file (I'm not a fan of the "no `using` statement" paradigm; it makes me feel like I'm coding in a sensory deprivation chamber). I'll build up the changes to this setup step by step (and then show the whole thing at the end so you can see the `using` statements too). 
 
    ```
    public static async Task Main(string[] args)
    {
         //initialization
         WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
    
        //other config stuff here
   
        //return
        await builder.Build().RunAsync();
    }
    ```

3. Since every thread you spin up will actually execute Program.cs (given that they are all proper Blazor contexts), we need something here to distinguish between our main app and our workers. As you'll see, we achieve that in very .NET Core-ish way via an environment variable (that you don't have to worry about setting yourself).

    ```
    public static async Task Main(string[] args)
    {
         //initialization
         WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
    
        //determine if this is a web worker
        if (builder.HostEnvironment.IsEnvironment(WebWorkerConstants.Hosting.WebWorker))
        {
            //worker thread
        }
        else
        {
            //main thread
        }
    
	    //other config stuff here
   
        //return
        await builder.Build().RunAsync();
    }
    ```

4. Let's take the main thread first in the `else` block above. All you need to do here is add the workers to your builder. Note that if there is SSR or other render mode logic happening on the backend, add this to your server project's `Program.cs` too.

    ```
    //main thread (or server startup logic)
    builder.AddWebWorkers();
    ```

5. As with any .NET core configuration adventure, there is an overload for options. Currently, all you can do here is tell the web worker infrastructure how to leverage Azure B2C auth tokens. As I get feedback around other folks' usage needs, I anticipate a lot will be added here!

    ```
    //main thread
    builder.AddWebWorkers(options =>
    {
        //configure web workers
        options.AzureB2CSettings.AppId = "<value>";
        options.AzureB2CSettings.Policy = "<value>";
        options.AzureB2CSettings.TenantId = "<value>"; 
        options.AzureB2CSettings.Instance = "<value>";
        options.AzureB2CSettings.AccessScope = "<value>";
    });
    ```

    **As always, like grandma taught you, be super careful when exposing server settings to the client. Never include app secrets, connection strings, or credentials of any kind!**

6. (Optional) If you'd like to kick the tires easily, I have included a demo service you can register. Anything in my repo that has "Demo" in the name is sample code (services, interfaces, and models) for testing; it in no way refers to the web workers or their infrastructure.

    ```
    //register a demo service for testing
    builder.Services.AddTransient<IDemoLongRunningService, DemoLongRunningService>();
    ```

7. Now for the affirming side of the `if` statement, this is how I literally shove Blazor into the web worker: 

    ```
    //worker thread
    await builder.UseWebWorkersAsync();
    ```

8. In order for a Blazor Web Worker to be aware of your dependency injection infrastructure so it can construct your services, you can use an overload of this method to configure your container. To keep this clean, I typically use extension methods to wrap common .NET core DI logic.

    ```
    //worker thread
    await builder.UseWebWorkersAsync((services, settingsService) =>
    {
        //register a demo service for testing
        services.AddTransient<IDemoLongRunningService, DemoLongRunningService>();
    });
    ``` 

Okay! Here's what my final Program.cs looks like, with all my obsessive use of regions, comments, and other code hygene that annoys my PR approvers. **Note:** in the code below, there are some usages of other helper libraries I typically include, but those are really just extension methods wrapping other DI logic or services that abstract `IConfigurationRoot` to make settings easier to consume; none of this is germane to the core example of how to leverage `AddWebWorkers` and `UseWebWorkers`.

```
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using LinesOfCode.Common;
using LinesOfCode.Web.Workers.Demo;
using LinesOfCode.Web.Client.Managers;
using LinesOfCode.Web.Workers.Managers;
using LinesOfCode.Web.Workers.Utilities;
using LinesOfCode.Domain.Common.Contracts.Managers;

namespace LinesOfCode.Web.Client
{
    /// <summary>
    /// This is the client WASM app.
    /// </summary>
    public class Program
    {
        #region Initialization
        /// <summary>
        /// This is the entry point to the app.
        /// </summary>
        public static async Task Main(string[] args)
        {
            //initialization
            WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

            //determine if this is a web worker
            if (builder.HostEnvironment.IsEnvironment(WebWorkerConstants.Hosting.WebWorker))
            {
                //register web workers
                await builder.UseWebWorkersAsync((services, settingsService) =>
                {
                    //register a demo service for testing
                    services.AddTransient<IDemoLongRunningService, DemoLongRunningService>();
                });
            }
            else
            {
                //build app
                await ClientDependencyManager.ConfigureBlazorWASMAppAsync(builder);
                ISettingsService settingsService = builder.Services.BuildServiceProvider(true).GetRequiredService<ISettingsService>();

                //add web worker services
                builder.AddWebWorkers(options =>
                {
                    //configure web workers
                    options.AzureB2CSettings.AppId = settingsService.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.AppId);
                    options.AzureB2CSettings.Policy = settingsService.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.Policy);
                    options.AzureB2CSettings.TenantId = settingsService.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.TenantId);
                    options.AzureB2CSettings.Instance = settingsService.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.Instance);
                    options.AzureB2CSettings.AccessScope = settingsService.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.AccessScope);
                });

                    //register a demo service for testing
                    services.AddTransient<IDemoLongRunningService, DemoLongRunningService>();
            }

            //other config stuff here

            //return
            await builder.Build().RunAsync();
        }
        #endregion
    }
}
```

## Usage

Let's see this thing in action. Here's an attempt to describe my approach in a few paragraphs instead of a full blog post. The idea is to use IL to generate a proxy class against any .NET interface that describes the shape of a service. All of the Reflection/Emit goodness in .NET works *just fine* in the browser!

These classes look identical to your concrete implementation as far as the interpreter is concerned; the difference is that the guts of each method are essentially scooped out and replaced with JavaScript interop logic that packages up the parameter values and sends them (along with a bunch of other refection-y invocation metadata) to a web worker that has been explicitly spooled up earlier in your code. Communication between the UI and worker threads is handled for you, and leverages [array buffers](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/ArrayBuffer) as [transferrable objects](https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API/Transferable_objects) for all JSON serialization to maximize performance.

When this message is received on the other side of your browser, more crazy reflection reassembles everything back into your real live concrete implementation that Blazor will happily execute as normal .NET browser code inside the Web Worker. Event callbacks and method invocation results (or errors) are then sent back to the main thread to update progress bars and/or gracefully finish your long running operations, allowing your users to keep Blazoring onward while your code runs in the background!

First, let's set up a quick Razor component to test this out (the details of my UI framework are outside the scope of this tutorial...but let's just say that Blazor + Material Design + Bootstrap 5 = frontend happiness). 

`<HotTake>`

Also, despite being 100% Italian, I **hate** spaghetti code; you'll therefore find my Blazor C# where it belongs, friends: in C# files. ;)

`</HotTake>`

```
@inherits WebWorkersBase

<Button Text="Create Web Worker" Click=@this.CreateWorkerAsync />

<div class=@LOCConstants.UI.CSS.Form.VerticalSpacerTop>
    <SpinForm Model=@this._selectedWorker>
        <div class="row">
            <div class="col-lg-4">
                <Dropdown B=@LinesOfCode.Web.Workers.Demo.DemoWorker @bind-Value=this._selectedWorker Data=@this._workers />
            </div>
            <div class="col-lg-2">
                <Textbox B=@int @bind-Value=this._simulationSeconds Min=1 Max=10 />
            </div>
            <div class="col-lg-2">
                <Checkbox @bind-Value=this._registerEvents />
            </div>
        </div>
    </SpinForm>   
</div>

<div class=@LOCConstants.UI.CSS.Form.VerticalSpacerTop>
    <Button Text="Run Web Worker" Click=@this.RunWorkerAsync />
</div>
```

Again, I can't go into all the details for the test harness and its usage of my many Blazor helpers, so the backend code will be segmented here so you can get a general idea of the the component's core dependencies and button click handlers. `IWebWorkerManager` is your main interface to creating and managing web worker threads. I also include a "real" (concrete) test service here so you can run it directly on the main thread for comparison.

```
#region Dependencies
[Inject()]
private IWebWorkerManager _webWorkerManager { get; set; }

[Inject()]
private IDemoLongRunningService _demoService { get; set; }
#endregion
```

Next, the `CreateWorkerAsync` button click handler, well, creates a web worker that's identified by a `Guid`. JavaScript threads could take a beat to load (especially on older machines; this is browser code, not server code); if you elect to spin up a bunch of threads at once when your app first loads, you can `await` all the resulting `Task` objects and send a callback to an overload of `CreateWorkerAsync`. This callback will let you know exactly when your threads are primed. I've even done this in my layout code and used memory state objects to politely enable and disable buttons across my app so they are only clickable when certain threads are created.

```
/// <summary>
/// Creates a new web worker.
/// </summary>
protected async Task CreateWorkerAsync()
{
    //initialization
    Guid id = Guid.NewGuid();

    //create worker
    CreateWorkerCallbackStatus status = await this._webWorkerManager.CreateWorkerAsync(id);
    if (status != CreateWorkerCallbackStatus.AlreadyInitializing)
    {
        //worker was successfully started or created
        this._selectedWorker = new DemoWorker(id);
        this._workers.Add(this._selectedWorker);
        await this.StateHasChangedAsync();

        //remember that it might not be available this moment; use the callback overload if you need to execute logic as soon as it's spooled up
    }
    else
    {
        //return
        await base.ShowToastAsync($"Unable to create worker {id}.", LogLevel.Error);
    }
}
```

Now let's run some code (the `RunDemoAsync` method on `DemoService` is my example function - again, you can proxy *any* method on *any* service without compromising any of Visual Studio's code search/refactoring functionality) on our new thread upon the other button click.

```
/// <summary>
/// Executes a new web worker.
/// </summary>
protected async Task RunWorkerAsync()
{
    //initialization
    this._simulationSeconds = Math.Min(Math.Max(this._simulationSeconds, 1), 10);

    //create a proxy
    IDemoLongRunningService proxiedDemoService = await this._webWorkerManager.GetProxyImplementationAsync<IDemoLongRunningService>(this._selectedWorker.Id);
    Guid invocationId = this._webWorkerManager.RegisterMethodInvocationCallbacks<IDemoLongRunningService, string>(proxiedDemoService, nameof(this._demoService.RunDemoAsync), this.WorkerCompletedAsync, this.WorkerFailedAsync);

    //handle events
    if (this._registerEvents)
        this._webWorkerManager.RegisterEventCallback<IDemoLongRunningService, DemoEventData>(proxiedDemoService, invocationId, nameof(this._demoService.DemoEvent), this.WorkerProgressedAsync);
          
    //return
    await proxiedDemoService.RunDemoAsync(this._simulationSeconds);   
}
```

Here's what's going on above:

 1. Call `await this._webWorkerManager.GetProxyImplementationAsync<YourInterface>(<yourWorkerId>);` to literally build a new concrete class that implements your interface and hand you back an instance of it that will feel just like your "real" service.
 
 2. Since these methods can't block the UI (which would kinda defeat the whole purpose of multithreading), the next step is to register callbacks to handle your method invocation's succees or failure. Simply create two async methods (one takes an object that's the result of your method call and a `Guid` that represents this specific invocation;  the other gets an error object with metadata about the problem and a representation of what was sent to the worker. In essence, when you invoke a proxy method call, instead of getting the result inline, you get it as a callback in a deferent method. This allows you to update state/show spinners/etc. so your UI remains responsive while the background code chugs. This code is a bit beefy, so first, here is the call extracted out and genericied a bit:

    `this._webWorkerManager.RegisterMethodInvocationCallbacks<YourInterface, YourMethodReturnType>(theInterfaceReturnedFromThePreviousCall, nameof(YourService.YourMethodName), this.YourSuccessfulCompletionMethod, this.YourFailedCompletionMethod);`

 3. And here are the callbacks (remember, `result` below can be of any type for `YourMethodReturnType` above!):
 
    ```
    /// <summary>
    /// This handles successful method proxy invocations.
    /// </summary>
    protected async Task WorkerCompletedAsync(string result, Guid invocationId)
    {
        //return
        if (string.IsNullOrWhiteSpace(result))
            await this.WorkerFailedAsync(new ErrorMessageModel(invocationId, null, "The worker completed successfully, but no result was provided."));
        else
            await base.ShowToastAsync($"Successfully completed proxy method invocation {invocationId}: {result}");
    }

    /// <summary>
    /// This handles failed method proxy invocations.
    /// </summary>
    protected async Task WorkerFailedAsync(ErrorMessageModel result)
    {
	    //initialization
        this._logger.LogError($"Unable to call {result.Proxy.MethodName} with {result.Proxy.ParameterTypeNames} and {result.Proxy.ParameterValues}.");

        //return
        if (!string.IsNullOrWhiteSpace(result?.Error))
            await base.ShowToastAsync($"Unable to complete proxy method invocation {result.InvocationId}: {result.Error}", LogLevel.Error);
        else
            await base.ShowToastAsync($"Unable to complete proxy method invocation {result.InvocationId}: An unknown error has occured.", LogLevel.Error);
    }
    ```
 
 4. Next, let's look at how progress events are handled. It's really the same idea: pass the proxy object, the return type and name of event, the method invocation id (which is a pattern that allows you to organize which events are handled on which methods) and an appropriate handler to `this._webWorkerManager.RegisterEventCallback`.

    ```
    //handle events
    if (this._registerEvents)
        this._webWorkerManager.RegisterEventCallback<IDemoLongRunningService, DemoEventData>(proxiedDemoService, invocationId, nameof(this._demoService.DemoEvent), this.WorkerProgressedAsync);
  
    ...
      
    /// <summary>
    /// This handles a method proxy event.
    /// </summary>
    protected async Task WorkerProgressedAsync(DemoEventData eventData, Guid invocationId)
    {
        //return
        if (eventData == null)
            await this.WorkerFailedAsync(new ErrorMessageModel(invocationId, null, "The worker event was handled successfully, but no data was provided."));
        else
            await base.ShowToastAsync($"Successfully handled proxy event {invocationId}: {eventData.ElapsedTime}");
    }            
    ```
 
 5. All that's left to do is execute your method! Notice that it's just like calling a normal function on a normal object; no orchestrators or wrappers are needed. As I've said, the only paradigmatic difference is that result doesn't come inline; it's sent to an event handler instead to really drive home how much your long running operation isn't blocking the UI!
 
     `await proxiedDemoService.RunDemoAsync(this._simulationSeconds);`

## New in 2.0

### Formal File Upload Support

A really cool use case is processing large/simultaneous image uploads in Blazor Web Workers. As long as you never actually touch the bytes in your .NET code and do it all with the crazy native JavaScript memory array APIs (with a little help from Azure Storage's [progressive upload API](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.specialized.blockblobclient.stageblockasync?view=azure-dotnet)) you can upload large images really quickly WITH accurate real-time progress bars.

The way this works is by using an overload of `IWebWorkerManager.GetProxyImplementationAsync` that accepts a second parameter of type `string` which is the `id` of an HTML file input control (or `InputFile` if you're using Blazor's HTML wrappers). If this is set, then invoking a method on your proxied service will include the "raw" native files sitting in the HTML control as additional transferrable objects that are sent to the Web Worker thread. As I mentioned above, what slows down the perceived performance in conventional Blazor usage is accessing an uploaded HTML file’s byte array via `IBrowserFile.OpenReadStream` from your .NET code running on the UI thread. This is especially true for very large (>10MB) files. By moving all I/O functionality to asynchronous JavaScript in a Web Worker thread leveraging `FileReader`, you never have to take a UI-blocking perf hit. 

Again, the details of this implementation are outside the scope of this readme, so I created a [quick demo app](https://blazor-web-workers.azurewebsites.net) to showcase this and other usage scenarios for Blazor Web Workers. 

### Mock Dependencies

With the interactive render modes added back in .NET 8, we have a lot more control over the dynamicism of our Blazor apps. Depending on your set up, .NET code can be running server-side, client-side, or both. Furthermore, you may or may not be using authentication; you may or may not be even running in a web context (e.g. perhaps you need to register your Web Workers in a middleware project that would be referenced in both web apps and web jobs). To enable as many scenarios as possible, the second release of Blazor Web Workers adds a few more options to `WebWorkerSettingsModel`:

- `UseMockAuthentication`: set this to `true` if your app is anonymous; otherwise you might get this love note from Blazor (since Web Workers takes a dependency on `AuthenticationStateProvider`): **Unhandled exception rendering component: Unable to resolve service for type 'Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider' while attempting to activate 'LinesOfCode.Web.Workers.Managers.WebWorkerManager'. System.InvalidOperationException: Unable to resolve service for type 'Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider' while attempting to activate 'LinesOfCode.Web.Workers.Managers.WebWorkerManager'.**.
- `UseMockNavigation`: another example of a non-web context is if you're using Blazor to render emails that are sent out as part of a nightly job in an Azure Function. You can still use Blazor in these scenarios, but it requires ensuring its interal dependencies are resolved. Setting this option to `true` injects a mocked implementation of `NavigationManager` so that your DI container can still resolve Razor components without the full web context.

Here's an example that sets both of these flags; depending on your setup, this might in either the server and/or client's `Program.cs`:

```
...

//add web worker services
builder.AddWebWorkers((options) =>
{
    //ready for anything!
    options.UseMockNavigation = true;
    options.UseMockAuthentication = true;
});

...
```

## Final Thoughts

Here are some tips and tricks as you start thinking about multithreaded JavaScript in Blazor.

 - There are more goodies around `IWebWorkerManager` to be aware of: a different override to handle void methods, logic to terminate and properly dispose of Web Workers, etc.
 - I highly recommend using `nameof` to reference method/event names instead of hardcoding strings for the various web worker registration APIs. Not only does this present better self-documenting code and resiliency to refactoring regressions, but it also serves the paradigm of these proxies feeling as close to “standard” concrete class implementations as possible. When I perform a “find all references” operation in Visual Studio, I should still be able to leverage my code’s telemetry irrespective to how I’m threading it out.
 - If you need authenticated Web Workers but your app has some anonymous pages/components that render before any login prompts, there is a way to "post-auth" Web Workers that have already been spun up using `IWebWorkerManager.SendAuthenticationTokenToWebWorkerAsync`.
 - Remember that Web Workers don't have access to the DOM or local storage, but they *can* access the browser's IndexedDB if you really *really* need to share state.
 - As I mentioned previously, there is a bit of native overhead to spooling up Web Worker threads; I've found that some of the lessons we had to learn when `async` came into our lives apply here too: it's sometimes faster to simply show a spinner and block the UI than to do this whole dance for an otherwise inexpensive operation.
 - Frankly, I haven't used this widely enough yet to establish firm best practices. However, one pattern that has been working well for me is to spin up about half a dozen threads when my app first loads  (which is done asynchronously; the last thing I want to do is further punish users waiting for Blazor WASM apps to load) and then really using the hell out of them, calling method after method and handling event after event versus spinning up a lot of one-use threads. While this *feels* "right" to me, it's anecdotal as this point until we have more data.
 - I've only tested this on Windows 10 and 11 with Chrome, Edge, and FireFox; please let me know if you encounter any environmental issues. I've also only ever used the authentication bits against a single Azure B2C tenant; I'm interested to see what other needs there are. (Different B2C configurations? Okta? Entra ID/AAD?)
 - I assume everyone is being a good little .NET Core developer and using `ILogger`, `IConfiguration`, etc. in their architectures. While I don't take a *hard* dependency on those technologies, I haven't tried this without them; just drop me an issue if any of this infrastructure adds unwanted bloat to your app (or simply breaks it). 

Wow that was *a lot* for a readme! As I said I'm really excited about this, and hope it will help keep our Blazor apps a blazin' until this gets gobbled up into .NET 10 or 17 or whatever. :) Thanks! 

<3 Domino

## Changelog

- **v1.0.0** - **v1.0.3**: Beta testing.
- **v1.0.4**: Initial realease.
- **v1.0.5** - **v1.0.8**: Bug fixes.
- **v1.1.0**: Added support for Azure B2C token refresh, which should happen silently behind the scenes. Authenticated Blazor Web Workers will catch any 401s and wait until a new token is sent. You can optionally set `WebWorkerSettingsModel.AzureB2CSettings.TokenRefreshTimeoutMilliseconds` in your startup code to control how long the web worker will block itself until a new access token is acquired from the main thread (assuming your Azure B2C + MSAL configuration supports token refresh). This defaults to five seconds, but this lets you increase it in case you need to do additional work to acquire the token. Finally, the `WebWorkerManager` will look for it in session storage and send it back to worker, who will then retry the failed 401 request one more time.
- **v2.0.0**: Upgraded to .NET 9, converted all (possible) JavaScript to use modules, and formalized file upload support.
    - *Breaking change*: `IWebWorkerManager.GetProxyImplementation` had to be async-ifed to `IWebWorkerManager.GetProxyImplementationAsync` so hopefully the fix is just a matter of sprinkling more `await` refactorings in your web worker manager code.
    - *Breaking change*: you'll need to remove any Web Worker script tags from `App.razor` (or other Blazor hosting HTML files) that were required in previous versions; moving the JavaScript to modules made this unnecessary.
- **v2.0.1** - **v2.0.4**: Beta testing.
- **v2.0.5**: 2.0 realease.
- **v2.0.6**: Added demo site (see "Project URL" in the Nuget properties).
- **v2.0.7**: Renamed "Mock" to "Demo" for the sample service so that the new `MockAuthenticationStateProvider` could be injected to fix certain anonymous issues. See the "Mock Dependencies" section above.
- **v2.0.8** - **v2.0.10**: Bug fixes.
- **v2.1.0** - Exposed `DependencyManager.GetJSRuntime` to allow components to access an instance of `IJSInProcessRuntime` for JavaScript interop.
- **v2.1.1** - Documentation updates.