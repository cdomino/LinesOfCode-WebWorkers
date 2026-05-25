# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

A NuGet package (`LinesOfCode.Web.Workers`, v2.5.0) that enables true multithreading in Blazor WebAssembly by running services inside browser Web Workers. The key innovation is IL-generated proxy classes (via `Reflection.Emit`) that intercept method calls on the main thread and marshal them to a worker, preserving IntelliSense and refactoring support for consuming apps.

## Build Commands

```powershell
# Build (also produces the NuGet package due to <GeneratePackageOnBuild>True</GeneratePackageOnBuild>)
dotnet build

# Build release NuGet package
dotnet build -c Release
```

There are no automated tests in this repo — the library is validated by running it in a consuming Blazor WASM app.

## Architecture

### Threading Model

The library runs two independent Blazor WASM runtimes in the same browser tab:

1. **Main thread** — the normal Blazor app. Hosts `WebWorkerManager`, creates worker proxies.
2. **Worker thread(s)** — each is a browser Web Worker that re-executes the app's `Program.cs`, but detects the `WebWorker` environment and calls `builder.UseWebWorkersAsync()` instead of the normal startup path.

### Data Flow for a Proxied Method Call

```
Main thread: proxy.MyMethod(args)
  → IL-generated proxy extracts method metadata + args
  → SerializationManager serializes to JSON → array buffer
  → JS: invokeWebWorker() posts transferable buffer to Worker

Worker thread: receives message
  → ProxyManager deserializes ProxyModel
  → Resolves real service from DI, invokes via reflection
  → Serializes result → array buffer → posts back to main

Main thread: JS callback fires
  → WebWorkerManager deserializes ResultMessageModel
  → Invokes the registered C# callback TaskCompletionSource
```

### Key Components

| Component | File | Role |
|-----------|------|------|
| `WebWorkerManager` | `Managers/WebWorkerManager.cs` | Main orchestrator: creates workers, generates IL proxies, manages callbacks via `ConcurrentDictionary` |
| `ProxyManager` | `Managers/ProxyManager.cs` | Runs inside worker: resolves DI services, invokes real methods, marshals results back |
| `DependencyManager` | `Managers/DependencyManager.cs` | Extension methods: `AddWebWorkers()` (main thread) and `UseWebWorkersAsync()` (worker thread) |
| `SerializationManager` | `Managers/SerializationManager.cs` | JSON serialization/deserialization for cross-thread message passing |
| `SettingsManager` | `Managers/SettingsManager.cs` | Passes configuration (Azure B2C, flags) from main to worker at startup |

### JavaScript Layer (`wwwroot/`)

- `web-worker-manager.js` — creates/terminates Worker instances, dispatches messages
- `web-worker-instance.js` — bootstraps a fake DOM (window, document, history) inside the Worker, then loads `blazor.webassembly.js`
- `web-worker-proxy.js` — handles `invokeWebWorker()` calls from C#, serializes invocations as transferable array buffers
- `web-worker-common.js` — shared buffer utilities and error handling
- `web-worker-environment.js` — DOM shims required for Blazor to load in a Worker context

### IL Proxy Generation

`WebWorkerManager.GetProxyImplementationAsync<T>()` uses `System.Reflection.Emit` to dynamically build a concrete class implementing interface `T`. Each method override extracts its own `MethodInfo` at runtime, packages it into a `ProxyModel`, and calls back into `WebWorkerManager` to dispatch it. This is the core differentiator vs. eval-based or source-gen approaches.

### Models

- `ProxyModel` — carries interface name, method name, serialized parameters, and return type across the thread boundary
- `ResultMessageModel` / `ErrorMessageModel` — success/failure responses from worker
- `EventHandlerMessageModel` — for services that raise events (progress reporting, etc.)
- `WebWorkerSettingsModel` — Azure B2C config, mock flags, passed to worker at startup

### Authentication

Azure B2C is supported. The main thread passes the current access token to the worker at creation time (`AzureB2CTokenModel`). Workers can request a token refresh via a dedicated message channel. When `UseMockAuthentication = true`, `MockAuthenticationStateProvider` is registered instead.

## Consuming App Integration

In a consuming Blazor WASM `Program.cs`:

```csharp
// Main thread startup
builder.AddWebWorkers();

// Worker thread startup (guard with environment check)
if (builder.HostEnvironment.Environment == "WebWorker")
{
    await builder.UseWebWorkersAsync();
    return;
}
```

Workers are created and proxies obtained at runtime:

```csharp
await _workerManager.CreateWorkerAsync("worker1");
var proxy = await _workerManager.GetProxyImplementationAsync<IMyService>("worker1");
var result = await proxy.DoWorkAsync(params);
```

## Package Publishing

The `.csproj` has `<GeneratePackageOnBuild>True</GeneratePackageOnBuild>` and `<PackageId>LinesOfCode.Web.Workers</PackageId>`. Build in Release to produce a publishable `.nupkg`. Version is set via `<Version>` in the `.csproj`.
