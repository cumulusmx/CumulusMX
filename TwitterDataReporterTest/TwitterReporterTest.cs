using System;
using System.Linq;
using Autofac;
using CumulusMX.Common;
using CumulusMX.Data;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using CumulusMXTest.Common;
using LinqToTwitter;
using TwitterDataReporter;
using UnitsNet;
using Xunit;

namespace TwitterDataReporterTest
{
    public class TwitterReporterTest : TestBase
    {
        public TwitterReporterTest()
        {

            var log = log4net.LogManager.GetLogger("cumulusTest", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            IConfigurationProvider testConfigurationProvider = new TestConfigurationProvider();
            var wrappedLogger = new LogWrapper(log);
            AutofacWrapper.Instance.Builder.RegisterInstance(wrappedLogger).As<ILogger>();
            DataStatistics = new WeatherDataStatistics();
            AutofacWrapper.Instance.Builder.RegisterInstance(DataStatistics).As<IWeatherDataStatistics>();
            AutofacWrapper.Instance.Builder.RegisterInstance(testConfigurationProvider).As<IConfigurationProvider>();
            AutofacWrapper.Instance.Builder.RegisterType<MeteoLib>().Named<object>("MeteoLib");

        }

        public WeatherDataStatistics DataStatistics { get; set; }

        [Fact]
        public void BasicMessageTest()
        {
            var log = log4net.LogManager.GetLogger("cumulusTest", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            var wrappedLogger = new LogWrapper(log);
            IConfigurationProvider testConfigurationProvider = new TestConfigurationProvider();
            var statistics = new WeatherDataStatistics();
            Type meteoLibType = typeof(MeteoLib);

            var di = GetDiWrapper(wrappedLogger, statistics, testConfigurationProvider, meteoLibType);
            di.Builder.RegisterType<TwitterDataReporter.TwitterDataReporter>();
            var testContext = new TwitterContextTest();
            testContext.Tag = System.Guid.NewGuid().ToString();
            di.Builder.RegisterInstance(testContext).As<ITwitterContext>();
            di.Builder.RegisterType(typeof(TwitterReporterSettings));
            di.Builder.RegisterType(typeof(XAuthoriserTest)).As<IAuthorizer>();
            di.Builder.RegisterType<XCredentialsTest>().As<ICredentialStore>();

            var reporter = di.Scope.Resolve<TwitterDataReporter.TwitterDataReporter>();
            reporter.DependencyInjection = di;
            reporter.Initialise();
            statistics.Add(new WeatherDataModel()
            {
                OutdoorTemperature = Temperature.FromDegreesCelsius(30),
                Pressure = Pressure.FromHectopascals(4000)
            });
            reporter.DoReport(statistics);
            Assert.Contains(testContext.Tweets, x => x.Status == "Temp 30");
        }
    }
}
