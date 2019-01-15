using System.Collections.Generic;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations
{
    public interface IStationSettingsService
    {
        IEnumerable<ExtensionSetting> GetCurrentSettings();
        IEnumerable<ExtensionSettingDescriptor> GetExtensionOptions();
        IStationSettings PopulateExtensionSettings(Dictionary<string, object> values);
    }
}