using System;
using System.Text.Json;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Models
{
    public class EventHandlerMessageModel : ResultMessageModel
    {
        #region Initialization
        [Obsolete(WebWorkerConstants.Messages.ObsoleteModelParameterlessConstructor)]
        public EventHandlerMessageModel() { }
        public EventHandlerMessageModel(Guid invocationId, ProxyModel proxy, JsonElement result, string eventName, string eventArgumentTypeName) : base(invocationId, proxy, result)
        {
            //initialization
            this.EventName = eventName;
            this.EventArgumentTypeName = eventArgumentTypeName;
        }
        #endregion
        #region Properties
        public string EventName { get; set; }
        public string EventArgumentTypeName { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.EventArgumentTypeName;
        }
        #endregion
    }
}
