using System.Linq;

namespace CumulusMXTest.Calculations
{
    public class TestBase
    {
        public TestBase()
        {
            if (log4net.LogManager.GetAllRepositories().Any(x => x.Name == "cumulus")) return;
            try
            {
                log4net.LogManager.CreateRepository("cumulus");
            }
            catch
            {
            }
        }
    }
}