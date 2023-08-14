using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

using EmbedIO;

using MySqlConnector;

using ServiceStack;

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
			var server = new JsonSettingsServer()
			{
				database = cumulus.MySqlConnSettings.Database,
				host = cumulus.MySqlConnSettings.Server,
				pass = cumulus.MySqlConnSettings.Password,
				port = cumulus.MySqlConnSettings.Port,
				user = cumulus.MySqlConnSettings.UserID
			};

			var monthly = new JsonSettingsMonthly()
			{
				enabled = cumulus.MySqlSettings.Monthly.Enabled,
				table = cumulus.MySqlSettings.Monthly.TableName
			};

			var reten = cumulus.MySqlSettings.RealtimeRetention.Split(' ');
			var retenVal = string.IsNullOrEmpty(reten[0]) ? 7 : int.Parse(reten[0]);
			var retenUnit = reten.Length > 1 && !string.IsNullOrEmpty(reten[1]) ? reten[1].ToUpper().TrimEnd('S') : "DAY";

			var realtime = new JsonSettingsRealtime()
			{
				enabled = cumulus.MySqlSettings.Realtime.Enabled,
				retentionVal = retenVal,
				retentionUnit = retenUnit,
				table = cumulus.MySqlSettings.Realtime.TableName,
				limit1min = cumulus.MySqlSettings.RealtimeLimit1Minute && cumulus.RealtimeInterval < 60000  // do not enable if real time interval is greater than 1 minute
			};

			var dayfile = new JsonSettingsDayfile()
			{
				enabled = cumulus.MySqlSettings.Dayfile.Enabled,
				table = cumulus.MySqlSettings.Dayfile.TableName
			};

			var customseconds = new JsonSettingsCustomSeconds()
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


			var customminutes = new JsonSettingsCustomMinutes()
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

			var customrollover = new JsonSettingsCustomRolloverStart()
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

			var customtimed = new JsonSettingsCustomTimed()
			{
				enabled = cumulus.MySqlSettings.CustomTimed.Enabled,
				entries = Array.Empty<JsonCustomTimed>()
			};

			cmdCnt = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomTimed.Commands[i]))
					cmdCnt++;
			}
			if (cmdCnt > 0)
			{
				customtimed.entries = new JsonCustomTimed[cmdCnt];

				index = 0;
				for (var i = 0; i < 10; i++)
				{
					customtimed.entries[index] = new JsonCustomTimed();

					if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomTimed.Commands[i]))
					{
						customtimed.entries[index].command = cumulus.MySqlSettings.CustomTimed.Commands[i];
						customtimed.entries[index].starttime = cumulus.MySqlSettings.CustomTimed.StartTimes[i];
						customtimed.entries[index].interval = cumulus.MySqlSettings.CustomTimed.Intervals[i];
						customtimed.entries[index].repeat = customtimed.entries[index].interval != 1440;
						index++;

						if (index == cmdCnt)
							break;
					}
				}
			}

			var customstartup = new JsonSettingsCustomRolloverStart()
			{
				enabled = cumulus.MySqlSettings.CustomStartUp.Enabled
			};

			cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomStartUp.Commands[i]))
					cmdCnt++;
			}
			customstartup.command = new string[cmdCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlSettings.CustomStartUp.Commands[i]))
					customstartup.command[index++] = cumulus.MySqlSettings.CustomStartUp.Commands[i];
			}

			var options = new JsonSettingsOptions()
			{
				updateonedit = cumulus.MySqlSettings.UpdateOnEdit,
				bufferonerror = cumulus.MySqlSettings.BufferOnfailure,
			};

			var data = new JsonSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				server = server,
				options = options,
				monthly = monthly,
				realtime = realtime,
				dayfile = dayfile,
				customseconds = customseconds,
				customminutes = customminutes,
				customrollover = customrollover,
				customtimed = customtimed,
				customstart = customstartup
			};

			return data.ToJson();
		}

		public string UpdateConfig(IHttpContext context)
		{
			string json = "";
			JsonSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonSettings>();
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
				cumulus.MySqlConnSettings.Server = String.IsNullOrWhiteSpace(settings.server.host) ? null : settings.server.host.Trim();
				if (settings.server.port > 0 && settings.server.port < 65536)
				{
					cumulus.MySqlConnSettings.Port = settings.server.port;
				}
				else
				{
					cumulus.MySqlConnSettings.Port = 3306;
				}
				cumulus.MySqlConnSettings.Database = String.IsNullOrWhiteSpace(settings.server.database) ? null : settings.server.database.Trim();
				cumulus.MySqlConnSettings.UserID = String.IsNullOrWhiteSpace(settings.server.user) ? null : settings.server.user.Trim();
				cumulus.MySqlConnSettings.Password = String.IsNullOrWhiteSpace(settings.server.pass) ? null : settings.server.pass.Trim();

				// options
				cumulus.MySqlSettings.UpdateOnEdit = settings.options.updateonedit;
				cumulus.MySqlSettings.BufferOnfailure = settings.options.bufferonerror;

				//monthly
				cumulus.MySqlSettings.Monthly.Enabled = settings.monthly.enabled;
				if (cumulus.MySqlSettings.Monthly.Enabled)
				{
					cumulus.MySqlSettings.Monthly.TableName = String.IsNullOrWhiteSpace(settings.monthly.table) ? "Monthly" : settings.monthly.table.Trim();
					if (cumulus.MySqlSettings.Monthly.TableName != cumulus.MonthlyTable.Name)
					{
						cumulus.MonthlyTable.Name = cumulus.MySqlSettings.Monthly.TableName;
						cumulus.MonthlyTable.Rebuild();
					}
				}
				//realtime
				cumulus.MySqlSettings.Realtime.Enabled = settings.realtime.enabled;
				if (cumulus.MySqlSettings.Realtime.Enabled)
				{
					cumulus.MySqlSettings.RealtimeRetention = settings.realtime.retentionVal + " " + settings.realtime.retentionUnit.Trim();
					cumulus.MySqlSettings.Realtime.TableName = String.IsNullOrWhiteSpace(settings.realtime.table) ? "Realtime" : settings.realtime.table.Trim();
					cumulus.MySqlSettings.RealtimeLimit1Minute = settings.realtime.limit1min;
					if (cumulus.MySqlSettings.Realtime.TableName != cumulus.RealtimeTable.Name)
					{
						cumulus.RealtimeTable.Name = cumulus.MySqlSettings.Realtime.TableName;
						cumulus.RealtimeTable.Rebuild();
					}
				}
				//dayfile
				cumulus.MySqlSettings.Dayfile.Enabled = settings.dayfile.enabled;
				if (cumulus.MySqlSettings.Dayfile.Enabled)
				{
					cumulus.MySqlSettings.Dayfile.TableName = String.IsNullOrWhiteSpace(settings.dayfile.table) ? "Dayfile" : settings.dayfile.table.Trim();
					if (cumulus.MySqlSettings.Dayfile.TableName != cumulus.DayfileTable.Name)
					{
						cumulus.DayfileTable.Name = cumulus.MySqlSettings.Dayfile.TableName;
						cumulus.DayfileTable.Rebuild();
					}
				}
				// custom seconds
				cumulus.MySqlSettings.CustomSecs.Enabled = settings.customseconds.enabled;
				if (cumulus.MySqlSettings.CustomSecs.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customseconds.command.Length)
							cumulus.MySqlSettings.CustomSecs.Commands[i] = String.IsNullOrWhiteSpace(settings.customseconds.command[i]) ? null : settings.customseconds.command[i].Trim();
						else
							cumulus.MySqlSettings.CustomSecs.Commands[i] = null;
					}

					cumulus.MySqlSettings.CustomSecs.Interval = settings.customseconds.interval;
				}
				// custom minutes
				cumulus.MySqlSettings.CustomMins.Enabled = settings.customminutes.enabled;
				if (cumulus.MySqlSettings.CustomMins.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customminutes.command.Length)
							cumulus.MySqlSettings.CustomMins.Commands[i] = String.IsNullOrWhiteSpace(settings.customminutes.command[i]) ? null : settings.customminutes.command[i].Trim();
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
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customrollover.command.Length)
							cumulus.MySqlSettings.CustomRollover.Commands[i] = String.IsNullOrWhiteSpace(settings.customrollover.command[i]) ? null : settings.customrollover.command[i].Trim();
						else
							cumulus.MySqlSettings.CustomRollover.Commands[i] = null;
					}
				}
				// custom timed
				cumulus.MySqlSettings.CustomTimed.Enabled = settings.customtimed.enabled;
				if (cumulus.MySqlSettings.CustomTimed.Enabled && null != settings.customtimed.entries)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customtimed.entries.Length)
						{
							cumulus.MySqlSettings.CustomTimed.Commands[i] = String.IsNullOrWhiteSpace(settings.customtimed.entries[i].command) ? null : settings.customtimed.entries[i].command.Trim();
							cumulus.MySqlSettings.CustomTimed.StartTimes[i] = settings.customtimed.entries[i].starttime;
							cumulus.MySqlSettings.CustomTimed.Intervals[i] = settings.customtimed.entries[i].interval;

						}
						else
						{
							cumulus.MySqlSettings.CustomTimed.Commands[i] = null;
							cumulus.MySqlSettings.CustomTimed.StartTimes[i] = TimeSpan.Zero;
							cumulus.MySqlSettings.CustomTimed.Intervals[i] = 0;

						}
					}
				}
				// custom start-up
				cumulus.MySqlSettings.CustomStartUp.Enabled = settings.customstart.enabled;
				if (cumulus.MySqlSettings.CustomStartUp.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customstart.command.Length)
							cumulus.MySqlSettings.CustomStartUp.Commands[i] = String.IsNullOrWhiteSpace(settings.customstart.command[i]) ? null : settings.customstart.command[i].Trim();
						else
							cumulus.MySqlSettings.CustomStartUp.Commands[i] = null;
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
			int cnt = 0;

			try
			{
				using (var mySqlConn = new MySqlConnection(cumulus.MySqlConnSettings.ToString()))
				{
					mySqlConn.Open();

					// first get a list of the columns the table currenty has
					var currCols = new List<string>();
					using (MySqlCommand cmd = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{table.Name}' AND TABLE_SCHEMA='{cumulus.MySqlConnSettings.Database}'", mySqlConn))
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

					var update = new StringBuilder("ALTER TABLE " + table.Name, 1024);
					foreach (var newCol in table.Columns)
					{
						if (!currCols.Contains(newCol.Name))
						{
							update.Append($" ADD COLUMN {newCol.Name} {newCol.Attributes},");
							cnt++;
						}
					}

					if (cnt > 0)
					{
						// strip trailing comma
						update.Length--;

						using (MySqlCommand cmd = new MySqlCommand(update.ToString(), mySqlConn))
						{
							int aff = cmd.ExecuteNonQuery();
							res = $"Added {cnt} columns to {table.Name} table";
							cumulus.LogMessage($"MySQL Update Table: " + res);
						}
					}
					else
					{
						res = $"The {table.Name} table already has all the required columns. Required = {table.Columns.Count}, actual = {currCols.Count}";
						cumulus.LogMessage("MySQL Update Table: " + res);
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
			return "{\"result\":\"" + CreateMySQLTable(cumulus.RealtimeTable.CreateCommand) + "\"}";
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

		private class JsonSettings
		{
			public bool accessible { get; set; }
			public JsonSettingsServer server { get; set; }
			public JsonSettingsOptions options { get; set; }
			public JsonSettingsMonthly monthly { get; set; }
			public JsonSettingsRealtime realtime { get; set; }
			public JsonSettingsDayfile dayfile { get; set; }
			public JsonSettingsCustomSeconds customseconds { get; set; }
			public JsonSettingsCustomMinutes customminutes { get; set; }
			public JsonSettingsCustomRolloverStart customrollover { get; set; }
			public JsonSettingsCustomTimed customtimed { get; set; }
			public JsonSettingsCustomRolloverStart customstart { get; set; }
		}

		private class JsonSettingsServer
		{
			public string host { get; set; }
			public uint port { get; set; }
			public string user { get; set; }
			public string pass { get; set; }
			public string database { get; set; }
		}

		private class JsonSettingsOptions
		{
			public bool updateonedit { get; set; }
			public bool bufferonerror { get; set; }
		}

		private class JsonSettingsMonthly
		{
			public bool enabled { get; set; }
			public string table { get; set; }
		}

		private class JsonSettingsRealtime
		{
			public bool enabled { get; set; }
			public string table { get; set; }
			public int retentionVal { get; set; }
			public string retentionUnit { get; set; }
			public bool limit1min { get; set; }
		}

		private class JsonSettingsDayfile
		{
			public bool enabled { get; set; }
			public string table { get; set; }
		}

		private class JsonSettingsCustomSeconds
		{
			public bool enabled { get; set; }
			public string[] command { get; set; }
			public int interval { get; set; }
		}

		private class JsonSettingsCustomMinutes
		{
			public bool enabled { get; set; }
			public string[] command { get; set; }
			public int intervalindex { get; set; }
		}

		private class JsonSettingsCustomRolloverStart
		{
			public bool enabled { get; set; }
			public string[] command { get; set; }
		}

		private class JsonSettingsCustomTimed
		{
			public bool enabled { get; set; }
			public JsonCustomTimed[] entries { get; set; }
		}

		private class JsonCustomTimed
		{
			public string command { get; set; }
			public int interval { get; set; }
			[IgnoreDataMember]
			public TimeSpan starttime { get; set; }

			[DataMember(Name = "starttimestr")]
			public string starttimestring
			{
				get => starttime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
				set => starttime = TimeSpan.ParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture);
			}
			public bool repeat { get; set; }
		}
	}
}
