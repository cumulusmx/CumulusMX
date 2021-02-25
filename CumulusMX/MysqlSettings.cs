using System;
using System.IO;
using System.Net;
using Devart.Data.MySql;
using ServiceStack;
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
			mySqlOptionsFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "MySqlOptions.json";
			mySqlSchemaFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "MySqlSchema.json";
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

			var monthly = new JsonMysqlSettingsMonthly()
			{
				table = cumulus.MySqlMonthlyTable
			};

			var realtime = new JsonMysqlSettingsRealtime()
			{
				retention = cumulus.MySqlRealtimeRetention,
				table = cumulus.MySqlRealtimeTable
			};

			var dayfile = new JsonMysqlSettingsDayfile()
			{
				table = cumulus.MySqlDayfileTable
			};

			var customseconds = new JsonMysqlSettingsCustomSeconds()
			{
				command = cumulus.CustomMySqlSecondsCommandString,
				interval = cumulus.CustomMySqlSecondsInterval
			};

			var customminutes = new JsonMysqlSettingsCustomMinutes()
			{
				command = cumulus.CustomMySqlMinutesCommandString,
				intervalindex = cumulus.CustomMySqlMinutesIntervalIndex
			};

			var customrollover = new JsonMysqlSettingsCustomRollover()
			{
				command = cumulus.CustomMySqlRolloverCommandString,
			};

			var data = new JsonMysqlSettings()
			{
				server = server,
				monthenabled = cumulus.MonthlyMySqlEnabled,
				monthly = monthly,
				realtimeenabled = cumulus.RealtimeMySqlEnabled,
				realtime = realtime,
				dayenabled = cumulus.DayfileMySqlEnabled,
				dayfile = dayfile,
				custsecsenabled = cumulus.CustomMySqlSecondsEnabled,
				customseconds = customseconds,
				custminsenabled = cumulus.CustomMySqlMinutesEnabled,
				customminutes = customminutes,
				custrollenabled = cumulus.CustomMySqlRolloverEnabled,
				customrollover = customrollover
			};

			return data.ToJson();
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
				var settings = json.FromJson<JsonMysqlSettings>();
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
				cumulus.MonthlyMySqlEnabled = settings.monthenabled;
				if (cumulus.MonthlyMySqlEnabled)
				{
					cumulus.MySqlMonthlyTable = String.IsNullOrWhiteSpace(settings.monthly.table) ? "Monthly" : settings.monthly.table;
				}
				//realtime
				cumulus.RealtimeMySqlEnabled = settings.realtimeenabled;
				if (cumulus.RealtimeMySqlEnabled)
				{
					cumulus.MySqlRealtimeRetention = settings.realtime.retention;
					cumulus.MySqlRealtimeTable = String.IsNullOrWhiteSpace(settings.realtime.table) ? "Realtime" : settings.realtime.table;
				}
				//dayfile
				cumulus.DayfileMySqlEnabled = settings.dayenabled;
				if (cumulus.DayfileMySqlEnabled)
				{
					cumulus.MySqlDayfileTable = String.IsNullOrWhiteSpace(settings.dayfile.table) ? "Dayfile" : settings.dayfile.table;
				}
				// custom seconds
				cumulus.CustomMySqlSecondsEnabled = settings.custsecsenabled;
				if (cumulus.CustomMySqlSecondsEnabled)
				{
					cumulus.CustomMySqlSecondsCommandString = settings.customseconds.command;
					cumulus.CustomMySqlSecondsInterval = settings.customseconds.interval;
				}
				// custom minutes
				cumulus.CustomMySqlMinutesEnabled = settings.custminsenabled;
				if (cumulus.CustomMySqlMinutesEnabled)
				{
					cumulus.CustomMySqlMinutesCommandString = settings.customminutes.command;
					cumulus.CustomMySqlMinutesIntervalIndex = settings.customminutes.intervalindex;
					if (cumulus.CustomMySqlMinutesIntervalIndex >= 0 && cumulus.CustomMySqlMinutesIntervalIndex < cumulus.FactorsOf60.Length)
					{
						cumulus.CustomMySqlMinutesInterval = cumulus.FactorsOf60[cumulus.CustomMySqlMinutesIntervalIndex];
					}
					else
					{
						cumulus.CustomMySqlMinutesInterval = 10;
					}
				}
				// custom rollover
				cumulus.CustomMySqlRolloverEnabled = settings.custrollenabled;
				if (cumulus.CustomMySqlRolloverEnabled)
				{
					cumulus.CustomMySqlRolloverCommandString = settings.customrollover.command;
				}

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
			cumulus.LogMessage($"MySQL Create Table: {createSQL}");

			string res;

			try
			{
				mySqlConn.Open();
				int aff = cmd.ExecuteNonQuery();
				cumulus.LogMessage($"MySQL Create Table: {aff} items were affected.");
				res = "Database table created successfully";
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("MySQL Create Table: Error encountered during MySQL operation.");
				cumulus.LogMessage(ex.Message);
				res = "Error: " + ex.Message;
			}
			finally
			{
				try
				{
					mySqlConn.Close();
				}
				catch {}
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
		public bool monthenabled { get; set; }
		public JsonMysqlSettingsMonthly monthly { get; set; }
		public bool realtimeenabled { get; set; }
		public JsonMysqlSettingsRealtime realtime { get; set; }
		public bool dayenabled { get; set; }
		public JsonMysqlSettingsDayfile dayfile { get; set; }
		public bool custsecsenabled { get; set; }
		public JsonMysqlSettingsCustomSeconds customseconds { get; set; }
		public bool custminsenabled { get; set; }
		public JsonMysqlSettingsCustomMinutes customminutes { get; set; }
		public bool custrollenabled { get; set; }
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
		public string table { get; set; }
	}

	public class JsonMysqlSettingsRealtime
	{
		public string table { get; set; }
		public string retention { get; set; }
	}

	public class JsonMysqlSettingsDayfile
	{
		public string table { get; set; }
	}

	public class JsonMysqlSettingsCustomSeconds
	{
		public string command { get; set; }
		public int interval { get; set; }
	}

	public class JsonMysqlSettingsCustomMinutes
	{
		public string command { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonMysqlSettingsCustomRollover
	{
		public string command { get; set; }
	}
}
