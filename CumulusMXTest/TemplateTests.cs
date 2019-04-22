using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autofac;
using AwekasDataReporter;
using CumulusMX;
using CumulusMX.Common;
using CumulusMX.Data;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using UnitsNet;
using Xunit;

namespace CumulusMXTest
{
    public class TemplateTests
    {
        private WeatherDataStatistics _statistics;
        private ILogger _log;
        private IDataReporterSettings _settings;

        public TemplateTests()
        {
            BuildTestStatistics();

            if (!log4net.LogManager.GetAllRepositories().Any(x => x.Name == "cumulus"))
                log4net.LogManager.CreateRepository("cumulus");
            
            _log = new LogWrapper(log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType));
            AutofacWrapper.Instance.Builder.RegisterInstance(_log).As<ILogger>();

            var configProvider = new TestConfigurationProvider {{"Defaults","TemperatureFormat","F3" }};
            AutofacWrapper.Instance.Builder.RegisterInstance(configProvider).As<IConfigurationProvider>();

            _settings = new DataReporterSettingsGeneric(configProvider);
        }

        private void BuildTestStatistics()
        {
            _statistics = new WeatherDataStatistics();
            var wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(20),
                OutdoorHumidity = Ratio.FromPercent(80),
                WindSpeed = Speed.FromKilometersPerHour(20),
                WindBearing = Angle.FromDegrees(45),
                SolarRadiation = Irradiance.FromKilowattsPerSquareCentimeter(10)
            };
            _statistics.Add(wdm);

            wdm = new WeatherDataModel()
            {
                Timestamp = DateTime.Parse("2019-04-01 18:45:10"),
                OutdoorTemperature = Temperature.FromDegreesCelsius(20.1),
                OutdoorHumidity = Ratio.FromPercent(79),
                WindSpeed = Speed.FromKilometersPerHour(18),
                WindBearing = Angle.FromDegrees(59),
                SolarRadiation = Irradiance.FromKilowattsPerSquareCentimeter(15)
            };
            _statistics.Add(wdm);
        }


        [Fact]
        public void SimplestTemplateTest()
        {
            var template = new StringReader("Value = 20.1");
            var renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            var output = renderer.Render();
            Assert.Equal("Value = 20.1", output);
        }

        [Fact]
        public void TimestampTemplateTest()
        {
            var timestamp = DateTime.Today;
            var template = new StringReader("Value = ${timestamp}");
            var renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            var output = renderer.Render();
            Assert.Equal($"Value = {timestamp.ToString("d",CultureInfo.InvariantCulture)}", output);
        }

        [Fact]
        public void TemperatureTemplateTest()
        {
            var timestamp = DateTime.Today;
            var template = new StringReader("Value = ${data.OutdoorTemperature.Latest.DegreesCelsius}");
            var renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            var output = renderer.Render();
            Assert.Equal($"Value = 20.1", output); 
        }

        [Fact]
        public void TemperatureImpliedUnitTemplateTest()
        {
            var template = new StringReader("Value = ${data.OutdoorTemperature.Latest}");
            var renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            var output = renderer.Render();
            Assert.Equal($"Value = 20.100", output);

            var configProvider = (TestConfigurationProvider)AutofacWrapper.Instance.Scope.Resolve<IConfigurationProvider>();
            configProvider.Add("Defaults", "TemperatureUnit", "Kelvin");
            template = new StringReader("Value = ${data.OutdoorTemperature.Latest}");
            renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            output = renderer.Render();
            Assert.Equal($"Value = 293.250", output);
        }

        [Fact]
        public void WindBeaufortTemplateTest()
        {
            var template = new StringReader("Value = ${data.WindSpeed.Latest.BeaufortDescription}");
            var renderer = new TemplateRenderer(template, _statistics, _settings, new Dictionary<string, object>(), _log);
            var output = renderer.Render();
            Assert.Equal($"Value = Light Breeze", output);
        }
    }
}
