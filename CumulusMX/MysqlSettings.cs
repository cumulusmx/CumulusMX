using System;
using System.IO;
using System.Net;
using MySqlConnector;
using ServiceStack;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class MysqlSettings
	{
		private readonly Cumulus cumulus;

		public MysqlSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormData()
		{
			var server = new JsonMysqlSettingsServer()
			{
				database = cumulus.MySqlConnSettings.Database,
				host = cumulus.MySqlConnSettings.Server,
				pass = cumulus.MySqlConnSettings.Password,
				port = cumulus.MySqlConnSettings.Port,
				user = cumulus.MySqlConnSettings.UserID
			};

			var monthly = new JsonMysqlSettingsMonthly()
			{
				enabled = cumulus.MySqlSettings.Monthly.Enabled,
				table = cumulus.MySqlSettings.Monthly.TableName
			};

			var reten = cumulus.MySqlSettings.RealtimeRetention.Split(' ');
			var retenVal = string.IsNullOrEmpty(reten[0]) ? 7 : int.Parse(reten[0]);
			var retenUnit = reten.Length > 1 && !string.IsNullOrEmpty(reten[1]) ? reten[1].ToUpper().TrimEnd('S') : "DAY";

			var realtime = new JsonMysqlSettingsRealtime()
			{
				enabled = cumulus.MySqlSettings.Realtime.Enabled,
				retentionVal = retenVal,
				retentionUnit = retenUnit,
				table = cumulus.MySqlSettings.Realtime.TableName,
				limit1min = cumulus.MySqlSettings.RealtimeLimit1Minute && cumulus.RealtimeInterval < 60000  // do not enable if real time interval is greater than 1 minute
			};

			var dayfile = new JsonMysqlSettingsDayfile()
			{
				enabled = cumulus.MySqlSettings.Dayfile.Enabled,
				table = cumulus.MySqlSettings.Dayfile.TableName
			};

			var customseconds = new JsonMysqlSettingsCustomSeconds()
			{
				enabled = cumulus.MySqlSettings.CustomSecs.Enabled,
				command = cumulus.MySqlSettings.CustomSecs.Command,
				interval = cumulus.MySqlSettings.CustomSecs.Interval
			};

			var customminutes = new JsonMysqlSettingsCustomMinutes()
			{
				enabled = cumulus.MySqlSettings.CustomMins.Enabled,
				command = cumulus.MySqlSettings.CustomMins.Command,
				intervalindex = cumulus.CustomMySqlMinutesIntervalIndex
			};

			var customrollover = new JsonMysqlSettingsCustomRollover()
			{
				enabled = cumulus.MySqlSettings.CustomRollover.Enabled,
				command = cumulus.MySqlSettings.CustomRollover.Command,
			};

			var options = new JsonMysqlSettingsOptions()
			{
				updateonedit = cumulus.MySqlSettings.UpdateOnEdit,
				bufferonerror = cumulus.MySqlSettings.BufferOnfailure,
			};

			var data = new JsonMysqlSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				server = server,
				options = options,
				monthly = monthly,
				realtime = realtime,
				dayfile = dayfile,
				customseconds = customseconds,
				customminutes = customminutes,
				customrollover = customrollover
			};

			return data.ToJson();
		}

		//public object UpdateMysqlConfig(HttpListenerContext context)
		public object UpdateConfig(IHttpContext context)
		{
			string json = "";
			JsonMysqlSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonMysqlSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing MySQL Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("MySQL Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating MySQL settings");

				// server
				cumulus.MySqlConnSettings.Server = settings.server.host;
				if (settings.server.port > 0 && settings.server.port < 65536)
				{
					cumulus.MySqlConnSettings.Port = settings.server.port;
				}
				else
				{
					cumulus.MySqlConnSettings.Port = 3306;
				}
				cumulus.MySqlConnSettings.Database = settings.server.database;
				cumulus.MySqlConnSettings.UserID = settings.server.user;
				cumulus.MySqlConnSettings.Password = settings.server.pass;

				// options
				cumulus.MySqlSettings.UpdateOnEdit = settings.options.updateonedit;
				cumulus.MySqlSettings.BufferOnfailure = settings.options.bufferonerror;

				//monthly
				cumulus.MySqlSettings.Monthly.Enabled = settings.monthly.enabled;
				if (cumulus.MySqlSettings.Monthly.Enabled)
				{
					cumulus.MySqlSettings.Monthly.TableName = String.IsNullOrWhiteSpace(settings.monthly.table) ? "Monthly" : settings.monthly.table;
				}
				//realtime
				cumulus.MySqlSettings.Realtime.Enabled = settings.realtime.enabled;
				if (cumulus.MySqlSettings.Realtime.Enabled)
				{
					cumulus.MySqlSettings.RealtimeRetention = settings.realtime.retentionVal + " " + settings.realtime.retentionUnit;
					cumulus.MySqlSettings.Realtime.TableName = String.IsNullOrWhiteSpace(settings.realtime.table) ? "Realtime" : settings.realtime.table;
					cumulus.MySqlSettings.RealtimeLimit1Minute = settings.realtime.limit1min;
				}
				//dayfile
				cumulus.MySqlSettings.Dayfile.Enabled = settings.dayfile.enabled;
				if (cumulus.MySqlSettings.Dayfile.Enabled)
				{
					cumulus.MySqlSettings.Dayfile.TableName = String.IsNullOrWhiteSpace(settings.dayfile.table) ? "Dayfile" : settings.dayfile.table;
				}
				// custom seconds
				cumulus.MySqlSettings.CustomSecs.Enabled = settings.customseconds.enabled;
				if (cumulus.MySqlSettings.CustomSecs.Enabled)
				{
					cumulus.MySqlSettings.CustomSecs.Command = settings.customseconds.command;
					cumulus.MySqlSettings.CustomSecs.Interval = settings.customseconds.interval;
				}
				// custom minutes
				cumulus.MySqlSettings.CustomMins.Enabled = settings.customminutes.enabled;
				if (cumulus.MySqlSettings.CustomMins.Enabled)
				{
					cumulus.MySqlSettings.CustomMins.Command = settings.customminutes.command;
					cumulus.CustomMySqlMinutesIntervalIndex = settings.customminutes.intervalindex;
					if (cumulus.CustomMySqlMinutesIntervalIndex >= 0 && cumulus.CustomMySqlMinutesIntervalIndex < cumulus.FactorsOf60.Length)
					{
						cumulus.MySqlSettings.CustomMins.Interval = cumulus.FactorsOf60[cumulus.CustomMySqlMinutesIntervalIndex];
					}
					else
					{
						cumulus.MySqlSettings.CustomMins.Interval = 10;
					}
				}
				// custom roll-over
				cumulus.MySqlSettings.CustomRollover.Enabled = settings.customrollover.enabled;
				if (cumulus.MySqlSettings.CustomRollover.Enabled)
				{
					cumulus.MySqlSettings.CustomRollover.Command = settings.customrollover.command;
				}

				// Save the settings
				cumulus.WriteIniFile();

				cumulus.SetMonthlySqlCreateString();
				cumulus.SetStartOfMonthlyInsertSQL();

				cumulus.SetDayfileSqlCreateString();
				cumulus.SetStartOfDayfileInsertSQL();

				cumulus.SetRealtimeSqlCreateString();
				cumulus.SetStartOfRealtimeInsertSQL();

				cumulus.CustomMysqlSecondsTimer.Interval = cumulus.MySqlSettings.CustomSecs.Interval * 1000;
				cumulus.CustomMysqlSecondsTimer.Enabled = cumulus.MySqlSettings.CustomSecs.Enabled;

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				var msg = "Error processing settings: " + ex.Message;
				cumulus.LogMessage(msg);
				context.Response.StatusCode = 500;
				return msg;
			}
			return "success";
		}

		private string CreateMySQLTable(string createSQL)
		{
			string res;
			using (var mySqlConn = new MySqlConnection(cumulus.MySqlConnSettings.ToString()))
			using (MySqlCommand cmd = new MySqlCommand(createSQL, mySqlConn))
			{
				cumulus.LogMessage($"MySQL Create Table: {createSQL}");

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
					catch
					{ }
				}
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
		public bool accessible {get; set;}
		public JsonMysqlSettingsServer server { get; set; }
		public JsonMysqlSettingsOptions options { get; set; }
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
		public uint port { get; set; }
		public string user { get; set; }
		public string pass { get; set; }
		public string database { get; set; }
	}

	public class JsonMysqlSettingsOptions
	{
		public bool updateonedit { get; set; }
		public bool bufferonerror { get; set; }
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
		public int retentionVal { get; set; }
		public string retentionUnit { get; set; }
		public bool limit1min { get; set; }
	}

	public class JsonMysqlSettingsDayfile
	{
		public bool enabled { get; set; }
		public string table { get; set; }
	}

	public class JsonMysqlSettingsCustomSeconds
	{
		public bool enabled { get; set; }
		public string command { get; set; }
		public int interval { get; set; }
	}

	public class JsonMysqlSettingsCustomMinutes
	{
		public bool enabled { get; set; }
		public string command { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonMysqlSettingsCustomRollover
	{
		public bool enabled { get; set; }
		public string command { get; set; }
	}
}
