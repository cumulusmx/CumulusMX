using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CumulusMX.Configuration;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations
{
    public class StationSettingsService : IStationSettingsService
    {
        private readonly IWeatherStation _station;
        private readonly IniConfigurationFile _iniFile;

        public StationSettingsService(IWeatherStation station, IniConfigurationFile iniFile)
        {
            this._station = station;
            this._iniFile = iniFile;
        }


        public IEnumerable<ExtensionSetting> GetCurrentSettings()
        {
            List<ExtensionSetting> list = new List<ExtensionSetting>();
            var instance = _station.ConfigurationSettings;

            var options = SettingsFactory.GetExtensionOptions(instance);
            var properties = SettingsFactory.GetWritableProperties(instance);
            foreach (var setting in options)
            {
                string configValue = _iniFile.GetValue(_station.Identifier, setting.Name, (string)null);
                var prop = properties.First(p => p.Name == setting.Name);
                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType is IConvertible)
                {
                    object value = Convert.ChangeType(configValue, prop.PropertyType);
                    var extensionSetting = new ExtensionSetting(setting.Name, setting.Group, setting.Description, value);
                    list.Add(extensionSetting);
                }
            }

            return list;
        }

        public IEnumerable<ExtensionSettingDescriptor> GetExtensionOptions()
        {
            var instance = _station.ConfigurationSettings;
            return SettingsFactory.GetExtensionOptions(instance);
        }


        public IStationSettings PopulateExtensionSettings(Dictionary<string, object> values)
        {
            var instance = _station.ConfigurationSettings;
            SettingsFactory.PopulateProperties(instance, values);
            return instance;
        }
    }
}
