using System.Collections.Generic;

using LinesOfCode.Web.Workers.Models;

namespace LinesOfCode.Web.Workers.Contracts
{
    public interface ISettingsManager
    {
        #region Properties
        bool IsWebWorker { get; }
        AzureB2CTokenModel Token { get; set; }
        #endregion
        #region Methods
        T GetSetting<T>(string key);
        T GetSection<T>(string key);
        Dictionary<string, string> GetAllSettings();
        #endregion
    }
}
