using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionDescriptor
    {
        public ExtensionDescriptor(string name, IExtension extension)
        {
            Name = name;
            Extension = extension;
        }

        public string Name { get; }
        public IExtension Extension { get; }
    }
}
