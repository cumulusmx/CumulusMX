using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CumulusMX.Data;
using CumulusMX.Common;
using CumulusMX.Data.Statistics;
using CumulusMX.Extensions.Station;
using CumulusMXTest.Common;
using UnitsNet;
using Xunit;

namespace CumulusMXTest.Calculations
{
    public class WeatherCalculationTests : TestBase
    {
        public WeatherCalculationTests()
        {

        }

        [Fact]
        public void HumidexTest()
        {
            var wds = new WeatherDataStatistics();
            wds.DefineStatistic("OutdoorTemperature", typeof(Temperature));
            wds.DefineStatistic("OutdoorHumidity", typeof(Ratio));
            wds.DefineStatistic("Humidex", typeof(Number));
            var method = typeof(MeteoLib).GetMethod("Humidex");
            wds.DefineCalculation("Humidex", new[] {"OutdoorTemperature", "OutdoorHumidity"}, method);

            var wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(20),
                OutdoorHumidity = Ratio.FromPercent(80)
            };
            wds.Add(wdm);

            Assert.Equal(20, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).DayMaximum.DegreesCelsius);
            Assert.Equal(0.8, ((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordMaximum.DecimalFractions);
            Assert.True((bool)((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordNow);

            var humidexResult = 20 + ((5.0 / 9.0) * (((80 * 6.112 * Math.Exp((17.62 * 20) / (243.12 + 20))) / 100.0) - 10.0));

            Assert.Equal(humidexResult, ((IStatistic<Number>)wds["Humidex"]).Latest.Value);

        }


        [Fact]
        public void RenameTest()
        {
            var wds = new WeatherDataStatistics();
            wds.DefineStatistic("OutdoorTemperature", typeof(Temperature));
            wds.DefineStatistic("OutdoorHumidity", typeof(Ratio));
            wds.DefineStatistic("LesTemps", typeof(Number));
            var method = typeof(MeteoLib).GetMethod("Humidex");
            wds.DefineCalculation("LesTemps", new[] { "OutdoorTemperature", "OutdoorHumidity" }, method);

            var wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(20),
                OutdoorHumidity = Ratio.FromPercent(1)
            };
            wds.Add(wdm);

            Assert.Equal(20, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).DayMaximum.DegreesCelsius);
            Assert.Equal(0.01, ((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordMaximum.DecimalFractions);
            Assert.True(((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordNow);

            var humidexResult = 20 + ((5.0 / 9.0) * (((1 * 6.112 * Math.Exp((17.62 * 20) / (243.12 + 20))) / 100.0) - 10.0));

            Assert.Equal(humidexResult, ((IStatistic<Number>)wds["LesTemps"]).Latest.Value);

        }


        [Fact]
        public void ApparentTemperatureTest()
        {
            var wds = new WeatherDataStatistics();
            wds.DefineStatistic("OutdoorTemperature", typeof(Temperature));
            wds.DefineStatistic("OutdoorHumidity", typeof(Ratio));
            wds.DefineStatistic("WindSpeed", typeof(Speed));
            wds.DefineStatistic("ApparentTemperature", typeof(Temperature));
            var method = typeof(MeteoLib).GetMethod("ApparentTemperature");
            wds.DefineCalculation("ApparentTemperature", new[] { "OutdoorTemperature", "WindSpeed", "OutdoorHumidity" }, method);

            var wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(43),
                OutdoorHumidity = Ratio.FromPercent(5),
                WindSpeed = Speed.FromKilometersPerHour(50)
                
            };
            wds.Add(wdm);
            wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:46"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(42.7),
                OutdoorHumidity = Ratio.FromPercent(7),
                WindSpeed = Speed.FromKilometersPerHour(70)

            };
            wds.Add(wdm);

            Assert.Equal(43, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).DayMaximum.DegreesCelsius);
            Assert.Equal(0.07, ((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordMaximum.DecimalFractions);

            double avp = 0.07 * 6.105 * Math.Exp(17.27 * 42.7 / (237.7 + 42.7));
            var apparentTempResult = 42.7 + (0.33 * avp) - (0.7 * 70000/60/60) - 4.0;

            Assert.Equal(apparentTempResult, ((IStatistic<Temperature>)wds["ApparentTemperature"]).Latest.Value,4);

        }

        [Fact]
        public void WindChillTest()
        {
            var wds = new WeatherDataStatistics();
            wds.DefineStatistic("OutdoorTemperature", typeof(Temperature));
            wds.DefineStatistic("OutdoorHumidity", typeof(Ratio));
            wds.DefineStatistic("WindSpeed", typeof(Speed));
            wds.DefineStatistic("WindChill", typeof(Temperature));
            var method = typeof(MeteoLib).GetMethod("WindChill");
            wds.DefineCalculation("WindChill", new[] { "OutdoorTemperature", "WindSpeed" }, method);

            var wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(43),
                OutdoorHumidity = Ratio.FromPercent(5),
                WindSpeed = Speed.FromKilometersPerHour(50)

            };
            wds.Add(wdm);
            wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:46"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(42.7),
                OutdoorHumidity = Ratio.FromPercent(7),
                WindSpeed = Speed.FromKilometersPerHour(70)

            };
            wds.Add(wdm);

            Assert.Equal(43, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).DayMaximum.DegreesCelsius);
            Assert.Equal(0.07, ((IStatistic<Ratio>)wds["OutdoorHumidity"]).RecordMaximum.DecimalFractions);

            Assert.Equal(42.7, ((IStatistic<Temperature>)wds["WindChill"]).Latest.DegreesCelsius, 4);
            wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:47"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(4.7),
                OutdoorHumidity = Ratio.FromPercent(7),
                WindSpeed = Speed.FromKilometersPerHour(70)

            };
            wds.Add(wdm);
            double windPow = Math.Pow(70, 0.16);
            double wcResult = 13.12 + (0.6215 * 4.7) - (11.37 * windPow) + (0.3965 * 4.7 * windPow);

            Assert.Equal(wcResult, ((IStatistic<Temperature>)wds["WindChill"]).Latest.DegreesCelsius, 4);
        }

        [Fact]
        public void CoolingDaysTest()
        {
            var wds = new WeatherDataStatistics();
            wds.DefineStatistic("OutdoorTemperature", typeof(Temperature));
            wds.DefineDayStatistic("CoolingDays","OutdoorTemperature", "Input.DayMaximum > Temperature.FromDegreesCelsius(27)");

            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-03-31 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(47) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-03-31 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(33) });
            wds.Add(new WeatherDataModel() {Timestamp = DateTime.Parse("2019-04-01 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(43)});
            wds.Add(new WeatherDataModel() {Timestamp = DateTime.Parse("2019-04-01 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(22)});
            wds.Add(new WeatherDataModel() {Timestamp = DateTime.Parse("2019-04-02 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(25)});
            wds.Add(new WeatherDataModel() {Timestamp = DateTime.Parse("2019-04-02 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(15)});
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-03 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(28) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-03 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(11) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-04 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(27.01) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-04 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(1) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-05 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(26.99) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-05 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(2) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-06 9:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(21) });
            wds.Add(new WeatherDataModel() { Timestamp = DateTime.Parse("2019-04-06 23:00"), OutdoorTemperature = Temperature.FromDegreesCelsius(25) });

            Assert.Equal(25, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).DayMaximum.DegreesCelsius);
            Assert.Equal(43, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).MonthMaximum.DegreesCelsius);
            Assert.Equal(47, ((IStatistic<Temperature>)wds["OutdoorTemperature"]).YearMaximum.DegreesCelsius);

            Assert.Equal(3, ((DayBooleanStatistic<Temperature>)wds["CoolingDays"]).MonthCount);
            Assert.Equal(4, ((DayBooleanStatistic<Temperature>)wds["CoolingDays"]).YearCount);
            Assert.Equal(Ratio.FromDecimalFractions(0.6), ((DayBooleanStatistic<Temperature>)wds["CoolingDays"]).MonthRatio);

        }


    }
}
