using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public interface IExtension
    {
        string Identifier { get; }
        void Initialise();
    }
}
