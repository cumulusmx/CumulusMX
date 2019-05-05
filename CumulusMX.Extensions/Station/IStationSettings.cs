using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public interface IStationSettings : ISettings
    {
        string ConfigurationSectionName { get; set; }
    }
}
