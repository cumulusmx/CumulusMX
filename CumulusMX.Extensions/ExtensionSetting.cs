using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionSettingAttribute : Attribute
    {
        public ExtensionSettingAttribute(string name, string description, string group, object defaultValue)
        {
            Name = name;
            Description = description;
            Group = group;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public string Description { get; }
        public string Group { get; }
        public object DefaultValue { get; }

    }
}
