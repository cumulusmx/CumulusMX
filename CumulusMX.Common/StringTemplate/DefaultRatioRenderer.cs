using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common.StringTemplate
{
    public class DefaultRatioRenderer: DefaultUnitRenderer<Ratio, RatioUnit>
    {
        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const RatioUnit GLOBAL_DEFAULT_UNIT = RatioUnit.Percent;
        private const string FORMAT_SETTING = "RatioFormat";
        private const string UNIT_SETTING = "RatioUnit";

        public DefaultRatioRenderer() : base(GetDefaults(GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT,FORMAT_SETTING,UNIT_SETTING))
        { }

        public DefaultRatioRenderer(string defaultFormat, RatioUnit defaultUnit) : base(defaultFormat,defaultUnit)
        {

        }
    }
}

