using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common.StringTemplate
{
    public class DefaultTemperatureRenderer: DefaultUnitRenderer<Temperature, TemperatureUnit>
    {
        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const TemperatureUnit GLOBAL_DEFAULT_UNIT = TemperatureUnit.DegreeCelsius;
        private const string FORMAT_SETTING = "TemperatureFormat";
        private const string UNIT_SETTING = "TemperatureUnit";

        public DefaultTemperatureRenderer() : base(GetDefaults(GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT,FORMAT_SETTING,UNIT_SETTING))
        { }

        public DefaultTemperatureRenderer(string defaultFormat, TemperatureUnit defaultUnit) : base(defaultFormat,defaultUnit)
        {

        }
    }
}

