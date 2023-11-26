using System.Collections.Generic;
using LinesOfCode.Web.Workers.Models;

namespace LinesOfCode.Web.Workers.Contracts.Services
{
    public interface ISettingsService
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
