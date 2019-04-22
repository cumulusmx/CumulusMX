using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common.StringTemplate
{
    public class DefaultPressureRenderer: DefaultUnitRenderer<Pressure, PressureUnit>
    {
        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const PressureUnit GLOBAL_DEFAULT_UNIT = PressureUnit.Kilopascal;
        private const string FORMAT_SETTING = "PressureFormat";
        private const string UNIT_SETTING = "PressureUnit";

        public DefaultPressureRenderer() : base(GetDefaults(GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT,FORMAT_SETTING,UNIT_SETTING))
        { }

        public DefaultPressureRenderer(string defaultFormat, PressureUnit defaultUnit) : base(defaultFormat,defaultUnit)
        {

        }
    }
}

