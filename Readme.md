# Blazor Web Workers
This provides a quick(ish) overview of how to use this package to achieve  *actual* multithreading in your Blazor web apps. Despite the yummy .NET `async`/`await`  goodness we've been enjoying in the browser for years now, JavaScript still only runs on the browser's main UI thread in general.
## Background 

Web Workers are the shortest path on the way to achieving parallelism in your browser .NET apps. Microsoft kind of flirted with this [as an experiment](https://visualstudiomagazine.com/articles/2022/10/11/blazor-webassembly-net7.aspx) while .NET 8 was still gestating, but it sadly didn't make the cut this round; Web Workers are therefore still not part of Blazor proper, which was the inspiration to publish this package. I have been evolving this code since I first got serious with Blazor WASM back in 2020, and now that I've ported everything to .NET 8 (and confirmed that the new Blazor Web App paradigm didn't completely send me back to the coding board) it's ready to share!

There are a few decent flavors of this kind of thing out there, but I decided to cook my own after not being able to accept the restrictions inherent to the initial offerings of other intrepid developers in this space. These challenges include requiring copying method parameters to local variables before they could be passed to worker, not being able to customize the copious serialization going on behind the scenes, not being able to share large/complex objects among the threads, and ultimately having an implementation that just didn't feel like a natural code flow.

I wanted my workers to be more of an implementation detail than a paradigm developers had to adopt. It was important to ~~my OCD~~ me to be able to take any service call and simply run it as a worker rather than having to contort my logic into a awkward posture that was agreeable to the multithreading frameworks out there I was attempting to leverage. There is still of course overhead and churn to anything this complicated, but I think I found the path of least fuckery to get up and running quickly.



Here are some other developer quality of life features I am excited to offer:

 - Both async and non async methods work (Async is the future, but let's not forget the past; a lot of third party libraries out there seem to have gone full async even when it's not necessary, making legacy sync code awkward to include).
 - VS tools like IntelliSense/Find Reference work as through the code was running on the main thread. Using "real" references in the library (verses `eval` or other approaches that could confuse a tree shaker) is specifically important for Blazor deployment code trimming; you probably don't want to wait 45 minutes for each build to finish before being able to see if your app was mutilated in the process because the trimmer didn't think your service's methods were  in use.
 - Since we can't debug into Blazor Web Worker  processes, I include lot of logging to help peek into the black (or at least WASM-colored) box.
 - I tried to keep the .NET Core-ish configuration bits as simple as possible; what you do in Program.cs is your business, not mine.
 - I need to be able to not only call APIs from workers, but also pass authentication tokens so remote resources  can be fetched securely as well.
 - Finally, only performing long running operations that do not block the UI isn't good enough; I provide  the ability to report progress via standard .NET events!

## Installation
The following steps walk you through how to get Blazor Web Workers installed and configured in your app.
 1. Install the Nuget package to your Blazor project (if you're using the .NET Core hosting model, you want the client app). I currently only support .NET 8 since my Thanksgiving break is only a week long, but if there's interest and you don't tell my wife, I would be happy to spend more time testing this against older frameworks.
 
 2. Throw this at the bottom of your `<body>...</body>` in App.razor (if you're using the new .NET 8 Blazor Web App templates; otherwise you want index.html for WASM or _Host.cshtml for Server):
 
`<script src="_content/LinesOfCode.Web.Workers/web-worker-manager.js"></script>`

3. Now move on to your Program.cs (which is the same logical file for startup code in all recent Blazor versions) and let's get into the tricky bits. Here is a "conventional" view of this file (I'm not a fan of the "no `using` statement" paradigm; it makes me feel like I'm coding in a sensory deprivation chamber). I'll go through the changes to this setup step by step (and then show the whole damn thing at the end so you can see the `using` statements too). 
```
public static async Task Main(string[] args)
{
     //initialization
     WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
    
    //other cool config stuff here
   
    //return
    await builder.Build().RunAsync();
}
```
4. First off, since every thread you spin up will actually run through Program.cs, we need something here to distinguish between our main app and our workers. As you'll see, we achieve that in very .NET Core-ish way via an environment variable (that you don't have to worry about setting yourself).

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
    
	//other cool config stuff here
   
    //return
    await builder.Build().RunAsync();
}
```
5. Let's take the main thread first in the `else` block above. All you need to do here is add the workers to your builder.
```
//main thread
builder.AddWebWorkers();
```
6. As with any .NET core configuration adventure, there is an overload for options. In this initial release, all you can do here is tell the web worker infrastructure how to work with Azure B2C auth tokens. As I get feedback around other folks' usage needs, I anticipate a lot will be added here!

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

7. (Optional) If you'd like to kick the tires easily, I have included a mock service you can register. Anything in my repo that has "Mock" in the name is sample code (services, interfaces, and models) for testing; it in no way refers to the web workers or their infrastructure.
```
 //register a mock service for testing
 builder.Services.AddTransient<IMockLongRunningService, MockLongRunningService>();
```
8. Now for the affirming side of the `if` statement, this is how I literally shove Blazor into the web worker: 
```
//worker thread
await builder.UseWebWorkersAsync();
```
9. (Also optional, kind of) If you opted into Step 7 above, you'll also have to make the web worker aware of our test harness using an overload.
```
//worker thread
await builder.UseWebWorkersAsync(services =>
{
    //register a mock service for testing
    services.AddTransient<IMockLongRunningService, MockLongRunningService>();
});
``` 
Okay! Here's what my final Program.cs looks like, with all my obsessive use of regions, comments, and other code hygene that annoys my PR approvers.
```
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using LinesOfCode.Common;
using LinesOfCode.Web.Workers.Mock;
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
                await builder.UseWebWorkersAsync(services =>
                {
                    //register a mock service for testing
                    services.AddTransient<IMockLongRunningService, MockLongRunningService>();
                });
            }
            else
            {
                //build app
                await ClientDependencyManager.ConfigureBlazorWASMAppAsync(builder);
                ISettingsManager settingsManager = builder.Services.BuildServiceProvider(true).GetRequiredService<ISettingsManager>();

                //add web worker services
                builder.AddWebWorkers(options =>
                {
                    //configure web workers
                    options.AzureB2CSettings.AppId = settingsManager.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.AppId);
                    options.AzureB2CSettings.Policy = settingsManager.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.Policy);
                    options.AzureB2CSettings.TenantId = settingsManager.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.TenantId);
                    options.AzureB2CSettings.Instance = settingsManager.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.Instance);
                    options.AzureB2CSettings.AccessScope = settingsManager.GetSetting<string>(LOCConstants.Settings.Security.AzureB2C.AccessScope);
                });

                //register a mock service for testing
                builder.Services.AddTransient<IMockLongRunningService, MockLongRunningService>();
            }

            //other cool config stuff here

            //return
            await builder.Build().RunAsync();
        }
        #endregion
    }
}
```

## Usage

Finally, let's see this thing in action. Here's an attempt to describe my approach in a few paragraphs instead of a full blog post. The idea is to use IL to generate a proxy class against any .NET interface that describes the shape of a service. All of the Reflection/Emit goodness in .NET works *just fine* in the browser!

These classes look identical to your concrete implementation as far as the interpreter is concerned; the difference is that the guts of each method are essentially scooped out and replaced with JavaScript that packages up the parameter values and sends them (along with a bunch of other refection-y invocation metadata) to a web worker that has been explicitly spooled up earlier in your code.

When this message is received on the other side of your browser, more crazy reflection reassembles everything back into your real live concrete implementation that Blazor will happily execute as normal .NET browser code inside the Web Worker. Event callbacks and method invocation results (or errors) are then sent back to the main thread to update progress bars and/or gracefully finish your long running operations, allowing your users to keep Blazoring onward while your code runs in the background!

First, let's set up a quick Razor component to test this out (the details of my UI framework are outside the scope of this tutorial...but let's just say that Blazor + Material Design + Bootstrap 5 = UI happiness). 

`<HotTake>`
Also, despite being 100% Italian, I **hate** spaghetti code, so you'll find my Blazor C# where it belongs, kids: in C# files.  
; )
`</HotTake>`
```
@inherits WebWorkersBase

<Button Text="Create Web Worker" Click=@this.CreateWorkerAsync />

<div class=@LOCConstants.UI.CSS.Form.VerticalSpacerTop>
    <SpinForm Model=@this._selectedWorker>
        <div class="row">
            <div class="col-lg-4">
                <Dropdown B=@LinesOfCode.Web.Workers.Mock.MockWorker @bind-Value=this._selectedWorker Data=@this._workers />
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
Again, I can't go into all the details for the test harness and its usage of my many Blazor helpers, but here are the main dependencies first; I'll then use the button click handlers as a nice way to segment the remaining  logic. `IWebWorkerManager` is your main interface to creating and managing web worker threads. I also include a "real" (concrete) test service here so you can run it directly on the main thread for comparison.
```
  #region Dependencies
  [Inject()]
  private IWebWorkerManager _webWorkerManager { get; set; }

  [Inject()]
  private IMockLongRunningService _mockService { get; set; }
  #endregion
```
Next, the `CreateWorkerAsync` button click handler, well, creates a web worker that's identified by a `Guid`. JavaScript threads could take a beat to load (especially on older machines; this is browser code, not server code); if you elect to spin up a bunch of threads at once when your app first loads, you can `await` all the `Task`s and send a callback to an overload of `CreateWorkerAsync` so that you know exactly when your threads are primed. I've even done this in my layout code and used memory state objects to politely enable and disable buttons across my app so they are only clickable when certain threads are created.
```
/// <summary>
/// Creates a new web worker.
/// </summary>
protected async Task CreateWorkerAsync()
{
    //initialization
    Guid id = Guid.NewGuid();

	//create worker (which returns true if the worker has already been created, false if it's in the initialization process, or null if creation was successful and is now in progress...apparently I'm unfamiliar with enums...)
	bool? result = await this._webWorkerManager.CreateWorkerAsync(id);
    if (result.GetValueOrDefault(true))
    {
        //worker was successfully started or created
		this._selectedWorker = new MockWorker(id);
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
Now let's run some code (the `RunAsync` method on `MockService` is my example function) on our new thread upon the other button click.
```
  /// <summary>
  /// Executes a new web worker.
  /// </summary>
  protected async Task RunWorkerAsync()
  {
      //initialization
      this._simulationSeconds = Math.Min(Math.Max(this._simulationSeconds, 1), 10);

      //create a proxy
      IMockLongRunningService proxiedMockService = this._webWorkerManager.GetProxyImplementation<IMockLongRunningService>(this._selectedWorker.Id);
      Guid invocationId = this._webWorkerManager.RegisterMethodInvocationCallbacks<IMockLongRunningService, string>(proxiedMockService, nameof(this._mockService.RunAsync), this.WorkerCompletedAsync, this.WorkerFailedAsync);

      //handle events
      if (this._registerEvents)
          this._webWorkerManager.RegisterEventCallback<IMockLongRunningService, MockEventData>(proxiedMockService, invocationId, nameof(this._mockService.MockEvent), this.WorkerProgressedAsync);
          
      //return
      await proxiedMockService.RunAsync(this._simulationSeconds);   
  }
```
Under the `else` are the major beats to hit for Blazor Web Workers:

 1. Call `this._webWorkerManager.GetProxyImplementation<YourInterface>(<yourWorkerId>);` which will literally build a new class that implements your interface and hand you back an instance of it that will feel just like your "real" service.
 
 2. Since these methods can't block the UI (which would kinda defeat the whole purpose of multithreading), the next step is to register callbacks for if your method invocation succeeds or fails. Simply create two async methods (one takes an object that's the result of your method call and a `Guid` that represents this specific invocation;  the other gets an error object with metadata about the problem and a representation of what was sent to the worker. In essence, when you invoke a proxy method call, instead of getting the result inline, you get it as a callback in a deferent method. This allows you to update state/show spinners/etc. so your UI remains responsive while the background code chugs. This code is a bit beefy, so first, here is the call extracted out and genericied a bit:

`this._webWorkerManager.RegisterMethodInvocationCallbacks<YourInterface, yourMethodReturnType>(theObjectReturnedFromThePreviousCall, nameof(yourService.yourMethodName), this.YourSuccessfulCompletionMethod, this.YourFailedCompletionMethod);`

3. And here are the callbacks (remember, the method result can be of any type!):
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
           await base.ShowToastAsync($"Unable to complete proxy method invocation {result?.InvocationId.ToString() ?? "N/A"}: An unknown error has occured.", LogLevel.Error);
   }
```
4. Next, let's look at how progress events are handled. It's really the same idea: pass the proxy object, the return type and name of event, the method invocation id (which is a pattern that allows you to organize which events are handled on which methods) and an appropriate handler to `this._webWorkerManager.RegisterEventCallback`.
```
  //handle events
  if (this._registerEvents)
      this._webWorkerManager.RegisterEventCallback<IMockLongRunningService, MockEventData>(proxiedMockService, invocationId, nameof(this._mockService.MockEvent), this.WorkerProgressedAsync);
  
  ...
      
  /// <summary>
  /// This handles a method proxy event.
  /// </summary>
  protected async Task WorkerProgressedAsync(MockEventData eventData, Guid invocationId)
  {
      //return
      if (eventData == null)
          await this.WorkerFailedAsync(new ErrorMessageModel(invocationId, null, "The worker event was handled successfully, but no data was provided."));
      else
          await base.ShowToastAsync($"Successfully handled proxy event {invocationId}: {eventData.ElapsedTime}");
  }            
```
5. All that's left to do is execute your method! ` await proxiedMockService.RunAsync(this._simulationSeconds);` Notice that it's just like calling a normal function on a normal object; no orchestrators or wrappers are needed. As I've said, the only paradigmatic difference is that result doesn't come inline; it's sent to an event handler instead to really drive home how much your long running operation isn't blocking the UI!

## Final Thoughts

Here are some tips and tricks as you start thinking about multithreaded JavaScript in Blazor.

 - There are more goodies around `IWebWorkerManager` to be aware of: a different override to handle void methods, logic to terminate and properly dispose of Web Workers, etc. 
 - If you need authenticated Web Workers but your app has some anonymous pages/components that before any login prompts, there is a way to "post-auth" Web Workers that have already been spun up using `IWebWorkerManager.SendAuthenticationTokenToWebWorkerAsync`.
 - Remember that Web Workers don't have access to the UI DOM or local storage, but they (somehow) *can* access the browser's IndexedDB if you really *really* need to share state.
 - As I mentioned previously, there is a bit of native overhead to spooling up Web Worker threads; I've found that some of the lessons we had to learn when `async` came into our lives apply here too: it's sometimes faster to simply show a spinner and block the UI than to do this whole dance for an otherwise simple operation.
 - Frankly, I haven't used this enough yet to establish firm best practices. However, one pattern that has been working well for me is to spin up about half a dozen threads when my app first loads  (which is done asynchronously; the last thing I want to do is further punish users waiting for Blazor WASM apps to load) and then really using the hell out of them, calling method after method and handling event after event versus spinning up a lot of one-use threads. While this *feels* "right" to me, it's anecdotal as this point until we have more data.
 - A really cool use case is processing image uploads in Blazor Web Workers. As long as you never actually touch the bytes in your .NET code and do it all with the crazy native JavaScript memory array APIs (and a little help from Azure Storage's [progressive upload API](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.specialized.blockblobclient.stageblockasync?view=azure-dotnet)) you can upload large images really quickly WITH real progress bars.
 - I've only tested this on Windows 11 with Chrome, Edge, and FireFox; please let me know if you encounter any environmental issues. I've also only ever used the authentication bits against a single Azure B2C tenant; I'm interested to see what other needs there are. (DIfferent B2C configurations? Okta? AAD?)
 - I assume everyone is being a good little .NET Core developer and using ILogger, IConfiguration, etc. in their architectures. While I don't take a *hard* dependency on those technologies, I haven't tried this without them; just drop me an issue if any of this infrastructure adds unwanted bloat to your app (or simply breaks it). 

Wow that was *a lot* for a read me! As I said I'm really excited about this, and hope it will help keep our Blazor apps a blazin' until this gets gobbled up into .NET 9 or 17 or whatever. :) Thanks! 

<3 Domino
