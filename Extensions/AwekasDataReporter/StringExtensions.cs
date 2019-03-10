using System.Text;

namespace AwekasDataReporter
{
    public static class StringExtensions
    {
        public static string ToHexadecimalString(
            this byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
    }
}
