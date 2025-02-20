using System;
using System.Text.Json;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Models
{
    public class ResultMessageModel : BaseMessageModel
    {
        #region Initialization
        [Obsolete(WebWorkerConstants.Messages.ObsoleteModelParameterlessConstructor)]
        public ResultMessageModel() { }
        public ResultMessageModel(Guid invocationId, ProxyModel proxy, JsonElement result) : base(invocationId, proxy)
        {
            //initialization
            this.Result = result;
        }
        #endregion
        #region Properties
        public JsonElement Result { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.InvocationId.ToString();
        }
        #endregion
    }
}
