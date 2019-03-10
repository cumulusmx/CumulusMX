using System;
using CumulusMX.Data;
using UnitsNet;
using UnitsNet.Units;
using Xunit;

namespace CumulusMXTest.Data
{
    public class MaxMinAverageUnitTest
    {
        [Fact]
        public void SimpleAverageTest()
        {
            var mmau = new MaxMinAverageUnit<Speed,SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromKilometersPerHour(60));
            mmau.AddValue(Speed.FromKilometersPerHour(70));
            Assert.Equal(60,mmau.Average.KilometersPerHour,3);
        }

        [Fact]
        public void SimpleMinimumTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromKilometersPerHour(60));
            mmau.AddValue(Speed.FromKilometersPerHour(70));
            Assert.Equal(50, mmau.Minimum.KilometersPerHour);
        }

        [Fact]
        public void SimpleMaximumTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromKilometersPerHour(60));
            mmau.AddValue(Speed.FromKilometersPerHour(70));
            Assert.Equal(70, mmau.Maximum.KilometersPerHour);
            
        }

        [Fact]
        public void SimpleNonZeroTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromKilometersPerHour(60));
            mmau.AddValue(Speed.FromKilometersPerHour(70));
            mmau.AddValue(Speed.Zero);
            Assert.Equal(3, mmau.NonZero);
            
        }

        [Fact]
        public void SimpleTotalTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromKilometersPerHour(60));
            mmau.AddValue(Speed.FromKilometersPerHour(70));
            Assert.Equal(180, mmau.Total.KilometersPerHour);
        }

        [Fact]
        public void MultiUnitAverageTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            mmau.AddValue(Speed.FromKilometersPerHour(50));
            mmau.AddValue(Speed.FromCentimetersPerSecond(1000)); //36 kph
            mmau.AddValue(Speed.FromKnots(30)); //55.56kph
            mmau.AddValue(Speed.Zero);
            Assert.Equal((50.0+36.0+55.56+0)/4, mmau.Average.KilometersPerHour,4);
        }

        [Fact]
        public void HighQuantityTest()
        {
            var mmau = new MaxMinAverageUnit<Speed, SpeedUnit>();
            for(int i=0;i<10001;i++)
                mmau.AddValue(Speed.FromKilometersPerHour(i));
            Assert.Equal(5000, mmau.Average.KilometersPerHour, 4);
            Assert.Equal(10000, mmau.NonZero);
        }

        [Fact]
        public void TemperatureTest()
        {
            var mmau = new MaxMinAverageUnit<Temperature, TemperatureUnit>();
            mmau.AddValue(Temperature.FromDegreesCelsius(50));
            mmau.AddValue(Temperature.FromKelvins(300)); //26.85 C
            mmau.AddValue(Temperature.FromDegreesFahrenheit(-40)); //-40C
            Assert.Equal((50.0 + 26.85 - 40) / 3, mmau.Average.DegreesCelsius, 4);
            Assert.Equal((-40), mmau.Minimum.DegreesCelsius, 4);
            Assert.Equal(50, mmau.Maximum.DegreesCelsius, 4);
        }

        [Fact]
        public void RatioTest()
        {
            var mmau = new MaxMinAverageUnit<Ratio, RatioUnit>();
            mmau.AddValue(new Ratio(0.5,RatioUnit.DecimalFraction));
            mmau.AddValue(new Ratio(73, RatioUnit.Percent));
            mmau.AddValue(new Ratio(50000, RatioUnit.PartPerMillion));
            Assert.Equal((50.0 + 73 + 5) / 3, mmau.Average.Percent, 4);
            Assert.Equal(73, mmau.Maximum.Percent, 4);
            Assert.Equal(5, mmau.Minimum.Percent, 4);
        }

        [Fact]
        public void AngleTest()
        {
            var mmau = new MaxMinAverageUnit<Angle, AngleUnit>();
            mmau.AddValue(Angle.FromDegrees(90));
            mmau.AddValue(Angle.FromDegrees(180));
            mmau.AddValue(Angle.FromRadians(1.5 * Math.PI)); // 270 degrees
            Assert.Equal((90.0 + 270.0 + 180.0) / 3, mmau.Average.Degrees, 4);
            Assert.Equal(270, mmau.Maximum.Degrees, 4);
            Assert.Equal(90, mmau.Minimum.Degrees, 4);
        }

        [Fact]
        public void PressureTest()
        {
            var mmau = new MaxMinAverageUnit<Pressure, PressureUnit>();
            mmau.AddValue(Pressure.FromBars(1000));
            mmau.AddValue(Pressure.FromDecibars(20000));
            mmau.AddValue(Pressure.FromHectopascals(50)); // 0.05 Bar
            Assert.Equal((1000 + 2000 + 0.05) / 3, mmau.Average.Bars, 4);
            Assert.Equal(2000, mmau.Maximum.Bars, 4);
            Assert.Equal(0.05, mmau.Minimum.Bars, 4);
        }

        [Fact]
        public void LengthTest()
        {
            var mmau = new MaxMinAverageUnit<Length, LengthUnit>();
            mmau.AddValue(Length.FromCentimeters(120)); //1.2m
            mmau.AddValue(Length.FromFeet(6)); // 1.8288m
            mmau.AddValue(Length.FromNauticalMiles(0.001)); //1.852m
            Assert.Equal((1.2 + 1.8288 + 1.852) / 3, mmau.Average.Meters, 2);
            Assert.Equal(1.852, mmau.Maximum.Meters, 2);
            Assert.Equal(1.2, mmau.Minimum.Meters, 2);
        }

        [Fact]
        public void IrradianceTest()
        {
            var mmau = new MaxMinAverageUnit<Irradiance, IrradianceUnit>();
            mmau.AddValue(Irradiance.FromKilowattsPerSquareMeter(100));
            mmau.AddValue(Irradiance.FromKilowattsPerSquareMeter(300));
            mmau.AddValue(Irradiance.FromKilowattsPerSquareMeter(400));
            Assert.Equal((100.0 + 300.0 + 400.0) / 3, mmau.Average.KilowattsPerSquareMeter, 4);
            Assert.Equal(400, mmau.Maximum.KilowattsPerSquareMeter, 4);
            Assert.Equal(100, mmau.Minimum.KilowattsPerSquareMeter, 4);
        }
        
    }
}
