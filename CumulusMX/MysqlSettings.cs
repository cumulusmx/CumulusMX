using System;
using System.IO;
using System.Net;
using Devart.Data.MySql;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class MysqlSettings
	{
		private readonly Cumulus cumulus;
		private readonly string mySqlOptionsFile;
		private readonly string mySqlSchemaFile;

		public MysqlSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			mySqlOptionsFile = AppDomain.CurrentDomain.BaseDirectory + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "MySqlOptions.json";
			mySqlSchemaFile = AppDomain.CurrentDomain.BaseDirectory + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "MySqlSchema.json";
		}

		public string GetMySqlAlpacaFormData()
		{
			var server = new JsonMysqlSettingsServer()
						 {
							 database = cumulus.MySqlDatabase,
							 host = cumulus.MySqlHost,
							 pass = cumulus.MySqlPass,
							 port = cumulus.MySqlPort,
							 user = cumulus.MySqlUser
						 };

			var monthly = new JsonMysqlSettingsMonthly() {enabled = cumulus.MonthlyMySqlEnabled, table = cumulus.MySqlMonthlyTable};

			var realtime = new JsonMysqlSettingsRealtime() {enabled = cumulus.RealtimeMySqlEnabled, retention = cumulus.MySqlRealtimeRetention, table = cumulus.MySqlRealtimeTable};

			var dayfile = new JsonMysqlSettingsDayfile() {enabled = cumulus.DayfileMySqlEnabled, table = cumulus.MySqlDayfileTable};

			var customseconds = new JsonMysqlSettingsCustomSeconds()
								{
									command = cumulus.CustomMySqlSecondsCommandString,
									enabled = cumulus.CustomMySqlSecondsEnabled,
									interval = cumulus.CustomMySqlSecondsInterval
								};

			var customminutes = new JsonMysqlSettingsCustomMinutes()
								{
									command = cumulus.CustomMySqlMinutesCommandString,
									enabled = cumulus.CustomMySqlMinutesEnabled,
									intervalindex = cumulus.CustomMySqlMinutesIntervalIndex
								};

			var customrollover = new JsonMysqlSettingsCustomRollover() {command = cumulus.CustomMySqlRolloverCommandString, enabled = cumulus.CustomMySqlRolloverEnabled};

			var data = new JsonMysqlSettings()
					   {
						   server = server,
						   monthly = monthly,
						   realtime = realtime,
						   dayfile = dayfile,
						   customseconds = customseconds,
						   customminutes = customminutes,
						   customrollover = customrollover
					   };

			return JsonConvert.SerializeObject(data);
		}

		public string GetMySqAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(mySqlOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetMySqAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(mySqlSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		//public object UpdateMysqlConfig(HttpListenerContext context)
		public object UpdateMysqlConfig(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = JsonConvert.DeserializeObject<JsonMysqlSettings>(json);
				// process the settings
				cumulus.LogMessage("Updating MySQL settings");

				// server
				cumulus.MySqlHost = settings.server.host;
				if (settings.server.port > 0 && settings.server.port < 65536)
				{
					cumulus.MySqlPort = settings.server.port;
				}
				else
				{
					cumulus.MySqlPort = 3306;
				}
				cumulus.MySqlDatabase = settings.server.database;
				cumulus.MySqlUser = settings.server.user;
				cumulus.MySqlPass = settings.server.pass;
				//monthly
				cumulus.MonthlyMySqlEnabled = settings.monthly.enabled;
				cumulus.MySqlMonthlyTable = settings.monthly.table;
				//realtime
				cumulus.RealtimeMySqlEnabled = settings.realtime.enabled;
				cumulus.MySqlRealtimeRetention = settings.realtime.retention;
				cumulus.MySqlRealtimeTable = settings.realtime.table;
				//dayfile
				cumulus.DayfileMySqlEnabled = settings.dayfile.enabled;
				cumulus.MySqlDayfileTable = settings.dayfile.table;
				// custom seconds
				cumulus.CustomMySqlSecondsCommandString = settings.customseconds.command;
				cumulus.CustomMySqlSecondsEnabled = settings.customseconds.enabled;
				cumulus.CustomMySqlSecondsInterval = settings.customseconds.interval;
				// custom minutes
				cumulus.CustomMySqlMinutesCommandString = settings.customminutes.command;
				cumulus.CustomMySqlMinutesEnabled = settings.customminutes.enabled;
				cumulus.CustomMySqlMinutesIntervalIndex = settings.customminutes.intervalindex;
				if (cumulus.CustomMySqlMinutesIntervalIndex >= 0 && cumulus.CustomMySqlMinutesIntervalIndex < cumulus.FactorsOf60.Length)
				{
					cumulus.CustomMySqlMinutesInterval = cumulus.FactorsOf60[cumulus.CustomMySqlMinutesIntervalIndex];
				}
				else
				{
					cumulus.CustomMySqlMinutesInterval = 10;
				}
				// custom rollover
				cumulus.CustomMySqlRolloverCommandString = settings.customrollover.command;
				cumulus.CustomMySqlRolloverEnabled = settings.customrollover.enabled;

				// Save the settings
				cumulus.WriteIniFile();

				cumulus.MonthlyMySqlConn.Host = cumulus.MySqlHost;
				cumulus.MonthlyMySqlConn.Port = cumulus.MySqlPort;
				cumulus.MonthlyMySqlConn.UserId = cumulus.MySqlUser;
				cumulus.MonthlyMySqlConn.Password = cumulus.MySqlPass;
				cumulus.MonthlyMySqlConn.Database = cumulus.MySqlDatabase;

				cumulus.SetMonthlySqlCreateString();
				cumulus.SetStartOfMonthlyInsertSQL();

				cumulus.SetDayfileSqlCreateString();
				cumulus.SetStartOfDayfileInsertSQL();

				cumulus.RealtimeSqlConn.Host = cumulus.MySqlHost;
				cumulus.RealtimeSqlConn.Port = cumulus.MySqlPort;
				cumulus.RealtimeSqlConn.UserId = cumulus.MySqlUser;
				cumulus.RealtimeSqlConn.Password = cumulus.MySqlPass;
				cumulus.RealtimeSqlConn.Database = cumulus.MySqlDatabase;

				cumulus.SetRealtimeSqlCreateString();
				cumulus.SetStartOfRealtimeInsertSQL();

				if (!String.IsNullOrEmpty(cumulus.MySqlRealtimeRetention))
				{
					cumulus.DeleteRealtimeSQL = "DELETE IGNORE FROM " + cumulus.MySqlRealtimeTable + " WHERE LogDateTime < DATE_SUB(NOW(), INTERVAL " + cumulus.MySqlRealtimeRetention + ")";
				}

				cumulus.CustomMysqlSecondsConn.Host = cumulus.MySqlHost;
				cumulus.CustomMysqlSecondsConn.Port = cumulus.MySqlPort;
				cumulus.CustomMysqlSecondsConn.UserId = cumulus.MySqlUser;
				cumulus.CustomMysqlSecondsConn.Password = cumulus.MySqlPass;
				cumulus.CustomMysqlSecondsConn.Database = cumulus.MySqlDatabase;
				cumulus.CustomMysqlSecondsTimer.Interval = cumulus.CustomMySqlSecondsInterval*1000;
				cumulus.CustomMysqlSecondsTimer.Enabled = cumulus.CustomMySqlSecondsEnabled;

				cumulus.CustomMysqlMinutesConn.Host = cumulus.MySqlHost;
				cumulus.CustomMysqlMinutesConn.Port = cumulus.MySqlPort;
				cumulus.CustomMysqlMinutesConn.UserId = cumulus.MySqlUser;
				cumulus.CustomMysqlMinutesConn.Password = cumulus.MySqlPass;
				cumulus.CustomMysqlMinutesConn.Database = cumulus.MySqlDatabase;

				cumulus.CustomMysqlRolloverConn.Host = cumulus.MySqlHost;
				cumulus.CustomMysqlRolloverConn.Port = cumulus.MySqlPort;
				cumulus.CustomMysqlRolloverConn.UserId = cumulus.MySqlUser;
				cumulus.CustomMysqlRolloverConn.Password = cumulus.MySqlPass;
				cumulus.CustomMysqlRolloverConn.Database = cumulus.MySqlDatabase;

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}

		private string CreateMySQLTable(string createSQL)
		{
			var mySqlConn = new MySqlConnection();
			mySqlConn.Host = cumulus.MySqlHost;
			mySqlConn.Port = cumulus.MySqlPort;
			mySqlConn.UserId = cumulus.MySqlUser;
			mySqlConn.Password = cumulus.MySqlPass;
			mySqlConn.Database = cumulus.MySqlDatabase;

			MySqlCommand cmd = new MySqlCommand();
			cmd.CommandText = createSQL;
			cmd.Connection = mySqlConn;
			cumulus.LogMessage(createSQL);

			string res;

			try
			{
				mySqlConn.Open();
				int aff = cmd.ExecuteNonQuery();
				cumulus.LogMessage("MySQL: " + aff + " rows were affected.");
				res = "Database table created successfully";
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error encountered during MySQL operation.");
				cumulus.LogMessage(ex.Message);
				res = "Error: " + ex.Message;
			}
			finally
			{
				mySqlConn.Close();
			}

			return res;
		}

		//public string CreateMonthlySQL(HttpListenerContext context)
		public string CreateMonthlySQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			string json = "{\"result\":\"" + CreateMySQLTable(cumulus.CreateMonthlySQL) + "\"}";
			return json;
		}

		//public string CreateDayfileSQL(HttpListenerContext context)
		public string CreateDayfileSQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			string json = "{\"result\":\"" + CreateMySQLTable(cumulus.CreateDayfileSQL) + "\"}";
			return json;
		}

		//public string CreateRealtimeSQL(HttpListenerContext context)
		public string CreateRealtimeSQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			string json = "{\"result\":\"" + CreateMySQLTable(cumulus.CreateRealtimeSQL) + "\"}";
			return json;
		}
	}

	public class JsonMysqlSettings
	{
		public JsonMysqlSettingsServer server { get; set; }
		public JsonMysqlSettingsMonthly monthly { get; set; }
		public JsonMysqlSettingsRealtime realtime { get; set; }
		public JsonMysqlSettingsDayfile dayfile { get; set; }
		public JsonMysqlSettingsCustomSeconds customseconds { get; set; }
		public JsonMysqlSettingsCustomMinutes customminutes { get; set; }
		public JsonMysqlSettingsCustomRollover customrollover { get; set; }
	}

	public class JsonMysqlSettingsServer
	{
		public string host { get; set; }
		public int port { get; set; }
		public string user { get; set; }
		public string pass { get; set; }
		public string database { get; set; }
	}

	public class JsonMysqlSettingsMonthly
	{
		public bool enabled { get; set; }
		public string table { get; set; }
	}

	public class JsonMysqlSettingsRealtime
	{
		public bool enabled { get; set; }
		public string table { get; set; }
		public string retention { get; set; }
	}

	public class JsonMysqlSettingsDayfile
	{
		public bool enabled { get; set; }
		public string table { get; set; }
	}

	public class JsonMysqlSettingsCustomSeconds
	{
		public string command { get; set; }
		public bool enabled { get; set; }
		public int interval { get; set; }
	}

	public class JsonMysqlSettingsCustomMinutes
	{
		public string command { get; set; }
		public bool enabled { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonMysqlSettingsCustomRollover
	{
		public string command { get; set; }
		public bool enabled { get; set; }
	}
}
