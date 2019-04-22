using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antlr4.StringTemplate.Debug;
using CumulusMX.Extensions;

namespace CumulusMXTest
{
    public class TestConfigurationProvider : IConfigurationProvider, IEnumerable
    {
        private Dictionary<(string,string), Setting> _settings;

        public TestConfigurationProvider()
        {
            _settings = new Dictionary<(string, string), Setting>();
        }

        public void Add(string sectionName, string key, string value)
        {
            SetValue(sectionName,key,new Setting(value));
        }

        public Setting GetValue(string sectionName, string key)
        {
            return _settings.FirstOrDefault(x => x.Key.Item1 == sectionName && x.Key.Item2 == key).Value;
        }

        public void SetValue(string sectionName, string key, Setting inSetting)
        {
            if (!_settings.ContainsKey((sectionName, key)))
                _settings[(sectionName, key)] = inSetting;
        }

        public Dictionary<string, Setting> GetSection(string sectionName)
        {
            return _settings.Where(x => x.Key.Item1 == sectionName).ToDictionary(x => x.Key.Item2, x => x.Value);
        }

        public IEnumerator GetEnumerator()
        {
            return _settings.GetEnumerator();
        }
    }

}