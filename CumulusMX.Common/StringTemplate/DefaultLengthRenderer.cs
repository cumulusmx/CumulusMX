using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common.StringTemplate
{
    public class DefaultLengthRenderer: DefaultUnitRenderer<Length, LengthUnit>
    {
        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const LengthUnit GLOBAL_DEFAULT_UNIT = LengthUnit.Millimeter;
        private const string FORMAT_SETTING = "LengthFormat";
        private const string UNIT_SETTING = "LengthUnit";

        public DefaultLengthRenderer() : base(GetDefaults(GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT, FORMAT_SETTING, UNIT_SETTING))
        { }

        public DefaultLengthRenderer(string defaultFormat, LengthUnit defaultUnit) : base(defaultFormat,defaultUnit)
        {

        }
    }
}