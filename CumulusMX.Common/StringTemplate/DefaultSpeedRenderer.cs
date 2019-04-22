using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common.StringTemplate
{
    public class DefaultSpeedRenderer: DefaultUnitRenderer<Speed, SpeedUnit>
    {
        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const SpeedUnit GLOBAL_DEFAULT_UNIT = SpeedUnit.KilometerPerHour;
        private const string FORMAT_SETTING = "SpeedFormat";
        private const string UNIT_SETTING = "SpeedUnit";

        public DefaultSpeedRenderer() : base(GetDefaults(GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT,FORMAT_SETTING,UNIT_SETTING))
        { }

        public DefaultSpeedRenderer(string defaultFormat, SpeedUnit defaultUnit) : base(defaultFormat,defaultUnit)
        {

        }
    }
}

