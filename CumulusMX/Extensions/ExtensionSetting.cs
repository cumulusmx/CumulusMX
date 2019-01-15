using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionSetting
    {
        public ExtensionSetting(string name, string description, string group, object value)
        {
            Name = name;
            Description = description;
            Group = group;
            Value = value;
        }

        public string Name { get; }
        public string Description { get; }
        public string Group { get; }
        public object Value { get; }

    }
}
