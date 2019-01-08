using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Swan;

namespace Cumulus4.Web
{
    public static class Api
    {
        private const string RelativePath = "/api/";
        internal static WeatherStation Station;
        public static StationSettings stationSettings;
        public static InternetSettings internetSettings;
        public static CalibrationSettings calibrationSettings;
        public static NOAASettings noaaSettings;
        public static MysqlSettings mySqlSettings;
        internal static DataEditor dataEditor;

        public static string Utf16ToUtf8(string utf16String)
        {
            /**************************************************************
             * Every .NET string will store text with the UTF16 encoding, *
             * known as Encoding.Unicode. Other encodings may exist as    *
             * Byte-Array or incorrectly stored with the UTF16 encoding.  *
             *                                                            *
             * UTF8 = 1 bytes per char                                    *
             *    ["100" for the ansi 'd']                                *
             *    ["206" and "186" for the russian 'κ']                   *
             *                                                            *
             * UTF16 = 2 bytes per char                                   *
             *    ["100, 0" for the ansi 'd']                             *
             *    ["186, 3" for the russian 'κ']                          *
             *                                                            *
             * UTF8 inside UTF16                                          *
             *    ["100, 0" for the ansi 'd']                             *
             *    ["206, 0" and "186, 0" for the russian 'κ']             *
             *                                                            *
             * We can use the convert encoding function to convert an     *
             * UTF16 Byte-Array to an UTF8 Byte-Array. When we use UTF8   *
             * encoding to string method now, we will get a UTF16 string. *
             *                                                            *
             * So we imitate UTF16 by filling the second byte of a char   *
             * with a 0 byte (binary 0) while creating the string.        *
             **************************************************************/

            // Storage for the UTF8 string
            string utf8String = String.Empty;

            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(utf16String);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Fill UTF8 bytes inside UTF8 string
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                // Because char always saves 2 bytes, fill char with 0
                byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
                utf8String += BitConverter.ToChar(utf8Container, 0);
            }

            // Return UTF8
            return utf8String;
        }

        private static string EscapeUnicode(string input)
        {
            StringBuilder sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (ch <= 0x7f)
                    sb.Append(ch);
                else
                    sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)ch);
            }
            return sb.ToString();
        }

        public static void Setup(WebServer server)
        {
            server.RegisterModule(new WebApiModule());

            server.Module<WebApiModule>().RegisterController<GraphDataController>(x => new GraphDataController(x));
            server.Module<WebApiModule>().RegisterController<DataController>();
            server.Module<WebApiModule>().RegisterController<RecordsController>();
            server.Module<WebApiModule>().RegisterController<TodayYestDataController>();
            server.Module<WebApiModule>().RegisterController<ExtraDataController>();
            server.Module<WebApiModule>().RegisterController<GetSettingsController>();
            server.Module<WebApiModule>().RegisterController<SetSettingsController>();
			server.Module<WebApiModule>().RegisterController<EditControllerGet>();
			server.Module<WebApiModule>().RegisterController<EditControllerPost>();
		}

        


        

    }
}
