using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.ExtensionMethods
{
    public interface IWeatherStation
    {
        string Manufacturer { get; }
        string Model { get; }
        string Description { get; set; }
        string APRSStationType { get; set; }
    }
}
