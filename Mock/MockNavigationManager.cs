using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace LinesOfCode.Web.Workers.Mock
{
    /// <summary>
    /// Provides a mock implementation of navigation manager.
    /// </summary>
    public class MockNavigationManager : NavigationManager, IHostEnvironmentNavigationManager
    {
        #region Public Methods
        /// <summary>
        /// This is not implemented for mock usage.
        /// </summary>
        void IHostEnvironmentNavigationManager.Initialize(string baseUri, string uri) {  }
        #endregion
    }
}
