using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionSettingAttribute : Attribute
    {
        public ExtensionSettingAttribute(string description, string group, object defaultValue)
        {
            Description = description;
            Group = group;
            DefaultValue = defaultValue;
        }

        public string Description { get; }
        public string Group { get; }
        public object DefaultValue { get; }

    }
}
