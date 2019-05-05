using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public class ExtensionIdentifierAttribute : Attribute
    {
        public ExtensionIdentifierAttribute(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }
    }
}
