using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CumulusMX.Extensions;

namespace CumulusMX.Common
{
    public class SettingsFactory
    {
        public static IEnumerable<ExtensionSettingDescriptor> GetExtensionOptions(object instance)
        {
            List<ExtensionSettingDescriptor> descriptors = new List<ExtensionSettingDescriptor>();
            var properties = GetWritableProperties(instance);

            foreach (var prop in properties)
            {
                var attribute = Enumerable.FirstOrDefault(prop.GetCustomAttributes(false), att => att.GetType() == typeof(ExtensionSettingAttribute)) as ExtensionSettingAttribute;
                if (attribute != null) {
                    descriptors.Add(new ExtensionSettingDescriptor(prop.Name, attribute.Description, attribute.Group, attribute.DefaultValue, prop.PropertyType,prop));
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

        public static void PopulateProperties(object instance, Dictionary<string, object> values)
        {
            var options = GetExtensionOptions(instance);

            var properties = GetWritableProperties(instance);
            foreach (var option in options)
            {
                if (values.ContainsKey(option.Name) && values[option.Name] != null)
                {
                    var property = properties.First(prop => prop.Name == option.Name);
                    //TODO: Check value is correct type
                    property.SetValue(instance, values[option.Name]);
                }
            }
        }

        public static void PopulateProperties(object instance, Dictionary<string, Setting> values)
        {
            var options = GetExtensionOptions(instance);

            foreach (var option in options)
            {
                if (values.ContainsKey(option.Name) && values[option.Name] != null)
                {
                    var property = option.Property;
                    object setValue = values[option.Name].AsType(option.Type) ?? option.DefaultValue;
                    property.SetValue(instance, setValue);
                }
                else
                {
                    option.Property.SetValue(instance,option.DefaultValue);
                }
            }
        }

        public static IEnumerable<PropertyInfo> GetWritableProperties(object instance)
        {
            var properties = instance.GetType().GetProperties().Where(prop => prop.CanWrite);
            return properties;
        }
    }
}