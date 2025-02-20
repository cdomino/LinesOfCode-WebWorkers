using System;
using System.Threading.Tasks;

using LinesOfCode.Web.Workers.Models;

namespace LinesOfCode.Web.Workers.Contracts
{
    public interface IWebWorkerManager
    {
        #region Properties
        Guid[] WebWorkerIds { get; }
        #endregion
        #region Members
        Task WorkerCreatedAsync(Guid workerId);
        Task TerminateWorkerAsync(Guid workerId);
        Task<bool> SendAuthenticationTokenToWebWorkerAsync(Guid workerId);
        I GetProxyImplementation<I>(Guid webWorkerId, string fileUploadControlId = null);
        Task<bool?> CreateWorkerAsync(Guid? workerId = null, Func<Guid, Task> createdCallback = null);
        Task<bool?> RegisterWorkerCreationCallbackAsync(Guid workerId, Func<Guid, Task> createdCallback);
        void RegisterVoidEventCallback<I>(I proxy, Guid invocationId, string eventName, Func<Guid, Task> eventCallback);
        void RegisterEventCallback<I, E>(I proxy, Guid invocationId, string eventName, Func<E, Guid, Task> eventCallback);
        Guid RegisterMethodInvocationCallbacks<I, R>(I proxy, string methodName, Func<R, Guid, Task> successCallback, Func<ErrorMessageModel, Task> errorCallback = null);
        Guid RegisterVoidMethodInvocationCallbacks<I>(I proxy, string methodName, Func<Guid, Task> successCallback = null, Func<ErrorMessageModel, Task> errorCallback = null);
        #endregion
    }
}
