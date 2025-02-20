using System;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Models
{
    public abstract class BaseMessageModel
    {
        #region Initialization
        [Obsolete(WebWorkerConstants.Messages.ObsoleteModelParameterlessConstructor)]
        public BaseMessageModel() { }
        public BaseMessageModel(Guid invocationId, ProxyModel proxy)
        {
            //initialization
            this.Proxy = proxy;
            this.InvocationId = invocationId;
        }
        #endregion
        #region Properties
        public ProxyModel Proxy { get; set; }
        public Guid InvocationId { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Proxy?.ToString() ?? "N/A";
        }
        #endregion
    }
}
