using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using LinesOfCode.Web.Workers.Models;
using LinesOfCode.Web.Workers.Contracts;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This provides access to the configuration superstructure.
    /// </summary>
    public class SettingsManager : ISettingsManager
    {
        #region Members
        private readonly IConfigurationRoot _config;
        #endregion
        #region Properties
        public bool IsWebWorker { get; private set; }
        public AzureB2CTokenModel Token { get; set; }
        #endregion
        #region Initialization
        public SettingsManager(IConfigurationRoot config, AzureB2CTokenModel token = null, bool isWebWorker = false)
        {
            //initialization
            this.Token = token;
            this._config = config;
            this.IsWebWorker = isWebWorker;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Gets a genericly-typed setting.
        /// </summary>
        public T GetSetting<T>(string key)
        {
            try
            {
                //return
                return this._config.GetValue<T>(key);
            }
            catch
            {
                //try as string
                string backupValue = this._config.GetValue<string>(key);

                //only throw if setting wasn't found
                if (backupValue == null)
                    throw new Exception($"Could not find a setting at {key}.");
                else
                    return default(T);
            }
        }

        /// <summary>
        /// Gets a genericly-typed section.
        /// </summary>
        public T GetSection<T>(string key)
        {
            try
            {
                //initialization
                IConfigurationSection section = this._config.GetSection(key);

                //return
                return section.Get<T>();
            }
            catch
            {
                //error
                return default(T);
            }
        }

        /// <summary>
        /// Gets all settings as a simple dictionary.
        /// </summary>
        public Dictionary<string, string> GetAllSettings()
        {
            //return
            return this._config.AsEnumerable().ToDictionary();
        }
        #endregion
    }
}
