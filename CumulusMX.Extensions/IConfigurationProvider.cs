using System.Collections.Generic;

namespace CumulusMX.Extensions
{
    public interface IConfigurationProvider
    {
        Setting GetValue(string sectionName, string key);
        void SetValue(string sectionName, string key, Setting inSetting);
        Dictionary<string,Setting> GetSection(string sectionName);
    }
}