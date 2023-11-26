using System.Linq;
using System.Collections.Generic;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Models
{
    public class ProxyModel
    {
        #region Initialization
        public ProxyModel()
        {
            //initialization
            this.ParameterValues = new Dictionary<string, object>();
            this.ParameterTypeNames = new Dictionary<string, string>();
        }
        #endregion
        #region Properties
        public string ReturnTypeName { get; set; }
        public string MethodName { get; set; }
        public string InterfaceType { get; set; }
        public string[] GenericTypeNames { get; set; }
        public Dictionary<string, object> ParameterValues { get; set; }
        public Dictionary<string, string> ParameterTypeNames { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //initialization
            string genericTypeDescription = (this.GenericTypeNames?.Any() ?? false) ? string.Empty : $"<{ this.GenericTypeNames.ToSeparatedList() }>";

            //return
            return $"{this.InterfaceType}.{this.MethodName}{genericTypeDescription}: {this.ParameterValues.ToDictionaryString()}";
        }

        /// <summary>
        /// Adds a parameter to the method call.
        /// </summary>
        public void AddParameter(string name, object value, string typeName)
        {
            //return
            this.ParameterValues.Add(name, value);
            this.ParameterTypeNames.Add(name, typeName);
        }
        #endregion
    }
}
