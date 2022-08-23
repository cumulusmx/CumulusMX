using System;
using System.IO;
using System.Net;
using MySqlConnector;
using ServiceStack;
using EmbedIO;
using System.Text;
using MySqlConnector.Logging;
using System.Collections.Generic;

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
				interval = cumulus.MySqlSettings.CustomSecs.Interval

			};

			var cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomSecs.Commands[i]))
					cmdCnt++;
			}
			customseconds.command = new string[cmdCnt];

			var index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomSecs.Commands[i]))
					customseconds.command[index++] = cumulus.MySqlSettings.CustomSecs.Commands[i];
			}


			var customminutes = new JsonMysqlSettingsCustomMinutes()
			{
				enabled = cumulus.MySqlSettings.CustomMins.Enabled,
				intervalindex = cumulus.CustomMySqlMinutesIntervalIndex
			};

			cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomMins.Commands[i]))
					cmdCnt++;
			}
			customminutes.command = new string[cmdCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomMins.Commands[i]))
					customminutes.command[index++] = cumulus.MySqlSettings.CustomMins.Commands[i];
			}

			var customrollover = new JsonMysqlSettingsCustomRollover()
			{
				enabled = cumulus.MySqlSettings.CustomRollover.Enabled
			};

			cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomRollover.Commands[i]))
					cmdCnt++;
			}
			customrollover.command = new string[cmdCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomRollover.Commands[i]))
					customrollover.command[index++] = cumulus.MySqlSettings.CustomRollover.Commands[i];
			}


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

		public string UpdateConfig(IHttpContext context)
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
					if (settings.monthly.table != cumulus.MonthlyTable.Name)
					{
						cumulus.MonthlyTable.Name = settings.monthly.table;
						cumulus.MonthlyTable.Rebuild();
					}
				}
				//realtime
				cumulus.MySqlSettings.Realtime.Enabled = settings.realtime.enabled;
				if (cumulus.MySqlSettings.Realtime.Enabled)
				{
					cumulus.MySqlSettings.RealtimeRetention = settings.realtime.retentionVal + " " + settings.realtime.retentionUnit;
					cumulus.MySqlSettings.Realtime.TableName = String.IsNullOrWhiteSpace(settings.realtime.table) ? "Realtime" : settings.realtime.table;
					cumulus.MySqlSettings.RealtimeLimit1Minute = settings.realtime.limit1min;
					if (settings.realtime.table != cumulus.RealtimeTable.Name)
					{
						cumulus.RealtimeTable.Name = settings.realtime.table;
						cumulus.RealtimeTable.Rebuild();
					}
				}
				//dayfile
				cumulus.MySqlSettings.Dayfile.Enabled = settings.dayfile.enabled;
				if (cumulus.MySqlSettings.Dayfile.Enabled)
				{
					cumulus.MySqlSettings.Dayfile.TableName = String.IsNullOrWhiteSpace(settings.dayfile.table) ? "Dayfile" : settings.dayfile.table;
					if (settings.dayfile.table != cumulus.DayfileTable.Name)
					{
						cumulus.DayfileTable.Name = settings.dayfile.table;
						cumulus.DayfileTable.Rebuild();
					}
				}
				// custom seconds
				cumulus.MySqlSettings.CustomSecs.Enabled = settings.customseconds.enabled;
				if (cumulus.MySqlSettings.CustomSecs.Enabled)
				{
					cumulus.MySqlSettings.CustomSecs.Commands[0] = settings.customseconds.command[0] ?? string.Empty;
					for (var i = 1; i < 10; i++)
					{
						if (i < settings.customseconds.command.Length)
							cumulus.MySqlSettings.CustomSecs.Commands[i] = settings.customseconds.command[i] ?? null;
						else
							cumulus.MySqlSettings.CustomSecs.Commands[i] = null;
					}

					cumulus.MySqlSettings.CustomSecs.Interval = settings.customseconds.interval;
				}
				// custom minutes
				cumulus.MySqlSettings.CustomMins.Enabled = settings.customminutes.enabled;
				if (cumulus.MySqlSettings.CustomMins.Enabled)
				{
					cumulus.MySqlSettings.CustomMins.Commands[0] = settings.customminutes.command[0] ?? string.Empty;
					for (var i = 1; i < 10; i++)
					{
						if (i < settings.customminutes.command.Length)
							cumulus.MySqlSettings.CustomMins.Commands[i] = settings.customminutes.command[i] ?? null;
						else
							cumulus.MySqlSettings.CustomMins.Commands[i] = null;
					}


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
					cumulus.MySqlSettings.CustomRollover.Commands[0] = settings.customrollover.command[0];
					for (var i = 1; i < 10; i++)
					{
						if (i < settings.customrollover.command.Length)
							cumulus.MySqlSettings.CustomRollover.Commands[i] = settings.customrollover.command[i] ?? null;
						else
							cumulus.MySqlSettings.CustomRollover.Commands[i] = null;
					}

				}

				// Save the settings
				cumulus.WriteIniFile();

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

		private string UpdateMySQLTable(MySqlTable table)
		{
			string res;
			int colsNow;
			int cnt = 0;

			try
			{
				using (var mySqlConn = new MySqlConnection(cumulus.MySqlConnSettings.ToString()))
				{
					mySqlConn.Open();

					// first get a list of the columns the table currenty has
					var currCols = new List<string>();
					using (MySqlCommand cmd = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{table.Name}' AND TABLE_SCHEMA='{cumulus.MySqlConnSettings.Database}'", mySqlConn))
					{
						using (MySqlDataReader reader = cmd.ExecuteReader())
						{
							if (reader.HasRows)
							{
								while (reader.Read())
								{
									var col = reader.GetString(0);
									currCols.Add(col);
								}
							}
						}
					}

					if (currCols.Count < table.Columns.Count)
					{
						var update = new StringBuilder("ALTER TABLE " + table.Name, 1024);
						foreach (var newCol in table.Columns)
						{
							if (!currCols.Contains(newCol.Name))
							{
								update.Append($" ADD COLUMN {newCol.Name} {newCol.Attributes},");
								cnt++;
							}
						}

						// strip trailing comma
						if (cnt > 0)
							update.Length--;

						using(MySqlCommand cmd = new MySqlCommand(update.ToString(), mySqlConn))
						{
							int aff = cmd.ExecuteNonQuery();
							cumulus.LogMessage($"MySQL Update Table: {aff} items were affected.");
							res = $"Added {cnt} columns to {table.Name} table";

						}
					}
					else
					{
						res = $"The {table.Name} already has the required columns = {table.Columns.Count}";
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("MySQL Update Table: Error encountered during MySQL operation.");
				cumulus.LogMessage(ex.Message);
				res = "Error: " + ex.Message;
			}

			return res;
		}

		public string CreateMonthlySQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			return "{\"result\":\"" + CreateMySQLTable(cumulus.MonthlyTable.CreateCommand) + "\"}";
		}

		public string CreateDayfileSQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			return "{\"result\":\"" + CreateMySQLTable(cumulus.DayfileTable.CreateCommand) + "\"}";
		}

		public string CreateRealtimeSQL(IHttpContext context)
		{
			context.Response.StatusCode = 200;
			return  "{\"result\":\"" + CreateMySQLTable(cumulus.RealtimeTable.CreateCommand) + "\"}";
		}

		public string UpdateMonthlySQL(IHttpContext context)
		{
			return "{\"result\":\"" + UpdateMySQLTable(cumulus.MonthlyTable) + "\"}";
		}

		public string UpdateDayfileSQL(IHttpContext context)
		{
			return "{\"result\":\"" + UpdateMySQLTable(cumulus.DayfileTable) + "\"}";
		}

		public string UpdateRealtimeSQL(IHttpContext context)
		{
			return "{\"result\":\"" + UpdateMySQLTable(cumulus.RealtimeTable) + "\"}";
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
		public string[] command { get; set; }
		public int interval { get; set; }
	}

	public class JsonMysqlSettingsCustomMinutes
	{
		public bool enabled { get; set; }
		public string[] command { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonMysqlSettingsCustomRollover
	{
		public bool enabled { get; set; }
		public string[] command { get; set; }
	}
}
