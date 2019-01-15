using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CumulusMX.Configuration;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations
{
    public class StationSettingsService : IStationSettingsService
    {
        private readonly IWeatherStation _station;
        private readonly IniFile _iniFile;

        public StationSettingsService(IWeatherStation station, IniFile iniFile)
        {
            this._station = station;
            this._iniFile = iniFile;
        }


        public IEnumerable<ExtensionSettingDescriptor> GetExtensionOptions()
        {
            List<ExtensionSettingDescriptor> descriptors = new List<ExtensionSettingDescriptor>();
            var instance = _station.ConfigurationSettings;
            var properties = GetWritableProperties(instance);

            foreach (var prop in properties)
            {
                var attribute = prop.GetCustomAttributes(false).FirstOrDefault(att => att.GetType() == typeof(ExtensionSettingAttribute)) as ExtensionSettingAttribute;
                if (attribute != null) {
                    descriptors.Add(new ExtensionSettingDescriptor(prop.Name, attribute.Description, attribute.Group, attribute.DefaultValue));
                }
            }
            return descriptors;


            //Dictionary<string, ExtensionSettingAttribute> attributes = new Dictionary<string, ExtensionSettingAttribute>();
            //var instance = _station.ConfigurationSettings;
            //var properties = GetWritableProperties(instance);
            //foreach (var prop in properties)
            //{
            //    var attribute = prop.GetCustomAttributes(false).FirstOrDefault(att => att.GetType() == typeof(ExtensionSettingAttribute));
            //    if (attribute != null)
            //        attributes.Add(prop.Name, (ExtensionSettingAttribute)attribute);
            //}
            //return attributes;
        }


        public IEnumerable<ExtensionSetting> GetCurrentSettings()
        {
            List<ExtensionSetting> list = new List<ExtensionSetting>();
            var instance = _station.ConfigurationSettings;

            var options = GetExtensionOptions();
            var properties = GetWritableProperties(instance);
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



        public IStationSettings PopulateExtensionSettings(Dictionary<string, object> values)
        {
            var options = GetExtensionOptions();

            var instance = _station.ConfigurationSettings;
            var properties = GetWritableProperties(instance);
            foreach (var option in options)
            {
                if (values.ContainsKey(option.Name) && values[option.Name] != null)
                {
                    var property = properties.First(prop => prop.Name == option.Name);
                    property.SetValue(instance, values[option.Name]);
                }
            }
            return instance;
        }


        private IEnumerable<PropertyInfo> GetWritableProperties(object instance)
        {
            var properties = instance.GetType().GetProperties(System.Reflection.BindingFlags.Public).Where(prop => prop.CanWrite);
            return properties;
        }
    }
}
