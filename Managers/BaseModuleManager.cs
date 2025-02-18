using System;
using System.Threading.Tasks;

using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This is the base class for all components that leverage JavaScript modules.
    /// </summary>
    public abstract class BaseModuleManager<T> : ComponentBase, IDisposable, IAsyncDisposable where T : class
    {
        #region Members
        private readonly IJSRuntime _jsRuntime;
        private readonly Lazy<Task<IJSObjectReference>> _module;
        protected DotNetObjectReference<BaseModuleManager<T>> _jsReference;
        #endregion
        #region Initialization
        public BaseModuleManager(IJSRuntime jsRuntime, string javascriptImportPath)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(javascriptImportPath))
                throw new ArgumentNullException(nameof(javascriptImportPath));

            //ensure dependencies
            this._jsReference = DotNetObjectReference.Create(this);
            this._jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(IJSRuntime));

            //return
            this._module = new Lazy<Task<IJSObjectReference>>(() =>
            {
                //lazy-import javascript module
                return jsRuntime.InvokeAsync<IJSObjectReference>(WebWorkerConstants.JavaScriptInterop.Functions.Import, javascriptImportPath).AsTask();
            });
        }
        #endregion       
        #region Public Methods
        /// <summary>
        /// Standard object clean up.
        /// </summary>
        public void Dispose()
        {
            //initialization
            this._jsReference?.Dispose();
        }

        /// <summary>
        /// Asynchronous object clean up.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            //initialization
            if (this._module.IsValueCreated)
            {
                //return
                IJSObjectReference module = await this.GetModuleAsync();
                await module.DisposeAsync();
            }
        }
        #endregion       
        #region Protected Methods
        /// <summary>
        /// Gets a reference to the module that can be used to issue JavaScript calls.
        /// </summary>
        protected async Task<IJSObjectReference> GetModuleAsync()
        {
            //return
            return await this._module.Value;
        }
        #endregion      
    }
}
