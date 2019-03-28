using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionSettingDescriptor
    {
        public ExtensionSettingDescriptor(string name, string description, string group, object defaultValue,Type settingType,PropertyInfo property)
        {
            Name = name;
            Description = description;
            Group = group;
            DefaultValue = defaultValue;
            Type = settingType;
            Property = property;
        }

        public string Name { get; }
        public string Description { get; }
        public string Group { get; }
        public object DefaultValue { get; }
        public Type Type { get; }
        public PropertyInfo Property { get; set; }
    }
}
