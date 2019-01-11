using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionSetting
    {
        public ExtensionSetting(string name, string description, string group, ExtensionSettingType type, object defaultValue)
        {
            Name = name;
            Description = description;
            Group = group;
            Type = type;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public string Description { get; }
        public string Group { get; }
        public ExtensionSettingType Type { get; }
        public object DefaultValue { get; }

    }
}
