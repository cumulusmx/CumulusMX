using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnitsNet;

namespace CumulusMX.Extensions.Station
{
    public class WeatherDataModel
    {
        private Dictionary<string, string> _mappings;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public Temperature? IndoorTemperature
        {
            get => (Temperature?)(Values.ContainsKey("IndoorTemperature") ? Values["IndoorTemperature"] : null);
            set => Values["IndoorTemperature"] = value;
        }

        public Temperature? OutdoorTemperature
        {
            get => (Temperature?)(Values.ContainsKey("OutdoorTemperature") ? Values["OutdoorTemperature"] : null);
            set => Values["OutdoorTemperature"] = value;
        }

        public Ratio? IndoorHumidity
        {
            get => (Ratio?)(Values.ContainsKey("IndoorHumidity") ? Values["IndoorHumidity"] : null);
            set => Values["IndoorHumidity"] = value;
        }

        public Ratio? OutdoorHumidity
        {
            get => (Ratio?)(Values.ContainsKey("OutdoorHumidity") ? Values["OutdoorHumidity"] : null);
            set => Values["OutdoorHumidity"] = value;
        }

        public Speed? WindGust
        {
            get => (Speed?)(Values.ContainsKey("WindGust") ? Values["WindGust"] : null);
            set => Values["WindGust"] = value;
        }

        public Speed? WindSpeed
        {
            get => (Speed?)(Values.ContainsKey("WindSpeed") ? Values["WindSpeed"] : null);
            set => Values["WindSpeed"] = value;
        }

        public Angle? WindBearing
        {
            get => (Angle?)(Values.ContainsKey("WindBearing") ? Values["WindBearing"] : null);
            set => Values["WindBearing"] = value;
        }

        public Pressure? Pressure
        {
            get => (Pressure?)(Values.ContainsKey("Pressure") ? Values["Pressure"] : null);
            set => Values["Pressure"] = value;
        }

        public Pressure? AltimeterPressure
        {
            get => (Pressure?)(Values.ContainsKey("AltimeterPressure") ? Values["AltimeterPressure"] : null);
            set => Values["AltimeterPressure"] = value;
        }

        public Temperature? OutdoorDewpoint
        {
            get => (Temperature?)(Values.ContainsKey("OutdoorDewpoint") ? Values["OutdoorDewpoint"] : null);
            set => Values["OutdoorDewpoint"] = value;
        }

        public Speed? RainRate
        {
            get => (Speed?)(Values.ContainsKey("RainRate") ? Values["RainRate"] : null);
            set => Values["RainRate"] = value;
        }

        public Length? RainCounter
        {
            get => (Length?)(Values.ContainsKey("RainCounter") ? Values["RainCounter"] : null);
            set => Values["RainCounter"] = value;
        }

        public Irradiance? SolarRadiation
        {
            get => (Irradiance?)(Values.ContainsKey("SolarRadiation") ? Values["SolarRadiation"] : null);
            set => Values["SolarRadiation"] = value;
        }

        public Number? UvIndex
        {
            get => GetTyped<Number>("UvIndex");
            set => Values["UvIndex"] = value;
        }

        public Temperature? ApparentTemperature
        {
            get => GetTyped<Temperature>("ApparentTemperature");
            set => Values["ApparentTemperature"] = value;
        }

        public Temperature? WindChill
        {
            get => GetTyped<Temperature>("WindChill");
            set => Values["WindChill"] = value;
        }

        public Temperature? HeatIndex
        {
            get => GetTyped<Temperature>("HeatIndex");
            set => Values["HeatIndex"] = value;
        }

        public Number? Humidex
        {
            get => GetTyped<Number>("Humidex");
            set => Values["Humidex"] = value;
        }

        public Dictionary<string,IQuantity> Values { get; set; }

        public Dictionary<string, string> Mappings
        {
            get
            {
                if (_mappings != null) return _mappings;
                return Values.Keys.ToDictionary(x => x, x => x);
            }
            set => _mappings = value;
        }

        public IEnumerable<string> Keys => Values.Keys;

        private TBase GetTyped<TBase>(string key) => (TBase)(Values.ContainsKey(key) ? Values[key] : null);

        public WeatherDataModel()
        {
            Values = new Dictionary<string, IQuantity>();
        }

        public IQuantity this[string observation] => GetTyped<IQuantity>(observation);


        #region OldContent
        //public string Forecast { get; set; }



        ///// <summary>
        ///// Peak wind gust in last 10 minutes
        ///// </summary>
        //public double? RecentMaxGust { get; set; }



        //public double? ET { get; set; }

        //public double? LightValue { get; set; }

        //public double? ChillHours { get; set; }

        //public double? midnightraincount { get; set; }

        //public int? MidnightRainResetDay { get; set; }


        ///// <summary>
        ///// Wind run for today
        ///// </summary>
        //public double? WindRunToday { get; set; }

        //public double? SunHourCounter { get; set; }

        //public double? StartOfDaySunHourCounter { get; set; }


        //public double? CurrentSolarMax { get; set; }

        //public double? RG11RainToday { get; set; }

        //public double? RainSinceMidnight { get; set; }
        #endregion

    }
}
