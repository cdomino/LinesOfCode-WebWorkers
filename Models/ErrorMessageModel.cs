using System;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Models
{
    public class ErrorMessageModel : BaseMessageModel
    {
        #region Initialization
        [Obsolete(WebWorkerConstants.Messages.ObsoleteModelParameterlessConstructor)]
        public ErrorMessageModel() { }
        public ErrorMessageModel(Guid invocationId, ProxyModel proxy, string error) : base(invocationId, proxy)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Properties
        public string Error { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Error;
        }
        #endregion
    }
}
