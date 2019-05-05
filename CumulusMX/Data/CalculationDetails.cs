using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace CumulusMX.Data
{
    
    internal class CalculationDetails
    {
        public IEnumerable<string> Inputs { get; set; }
        public string Measure { get; set; }
        public MethodInfo Method { get; set; }
    }
}