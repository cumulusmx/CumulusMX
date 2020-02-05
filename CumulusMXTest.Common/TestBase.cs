using System;
using System.Linq;
using Autofac;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace CumulusMXTest.Common
{
    public class TestBase
    {
        public TestBase()
        {
            if (log4net.LogManager.GetAllRepositories().Any(x => x.Name == "cumulusTest")) return;
            try
            {
                log4net.LogManager.CreateRepository("cumulusTest");
            }
            catch
            {
                // Do nothing - failure probably means the repository already exists
            }
        }

        protected internal static AutofacWrapper GetDiWrapper(LogWrapper wrappedLogger, IWeatherDataStatistics statistics,
            IConfigurationProvider testConfigurationProvider, Type meteoLibType)
        {
            //TODO: Set defaults when null passed
            var di = AutofacWrapper.GetLocalInstance();
            di.Builder.RegisterInstance(wrappedLogger).As<ILogger>();
            di.Builder.RegisterInstance(statistics).As<IWeatherDataStatistics>();
            di.Builder.RegisterInstance(testConfigurationProvider).As<IConfigurationProvider>();
            di.Builder.RegisterType(meteoLibType).Named<object>("MeteoLib");
            return di;
        }
    }
}