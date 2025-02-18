using System.Threading.Tasks;
using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace LinesOfCode.Web.Workers.Mock
{
    /// <summary>
    /// This is a mocked concerete auth state provider implementation.
    /// </summary>
    public class MockAuthenticationStateProvider : AuthenticationStateProvider
    {
        #region Public Methods
        /// <summary>
        /// This is not implemented for mock usage.
        /// </summary>
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            //return
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
        }
        #endregion
    }
}
