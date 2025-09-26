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

namespace CumulusMX.Settings
{
	public class MysqlSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{
			var advanced = new SettingsServerAdvanced()
			{
				sslMode = (uint) cumulus.MySqlFuncs.MySqlConnSettings.SslMode,
				tlsVers = cumulus.MySqlFuncs.MySqlConnSettings.TlsVersion
			};


			var server = new JsonSettingsServer()
			{
				database = cumulus.MySqlFuncs.MySqlConnSettings.Database,
				host = cumulus.MySqlFuncs.MySqlConnSettings.Server,
				pass = cumulus.MySqlFuncs.MySqlConnSettings.Password,
				port = cumulus.MySqlFuncs.MySqlConnSettings.Port,
				user = cumulus.MySqlFuncs.MySqlConnSettings.UserID,
				advanced = advanced
			};

			var monthly = new JsonSettingsMonthly()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.Monthly.Enabled,
				table = cumulus.MySqlFuncs.MySqlSettings.Monthly.TableName
			};

			var reten = cumulus.MySqlFuncs.MySqlSettings.RealtimeRetention.Split(' ');
			var retenVal = string.IsNullOrEmpty(reten[0]) ? 7 : int.Parse(reten[0]);
			var retenUnit = reten.Length > 1 && !string.IsNullOrEmpty(reten[1]) ? reten[1].ToUpper().TrimEnd('S') : "DAY";

			var realtime = new JsonSettingsRealtime()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.Realtime.Enabled,
				retentionVal = retenVal,
				retentionUnit = retenUnit,
				table = cumulus.MySqlFuncs.MySqlSettings.Realtime.TableName,
				limit1min = cumulus.MySqlFuncs.MySqlSettings.RealtimeLimit1Minute && cumulus.RealtimeInterval < 60000  // do not enable if real time interval is greater than 1 minute
			};

			var dayfile = new JsonSettingsDayfile()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.Dayfile.Enabled,
				table = cumulus.MySqlFuncs.MySqlSettings.Dayfile.TableName
			};

			var customseconds = new JsonSettingsCustomSeconds()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Enabled,
				interval = cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Interval
			};

			var cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Commands[i]))
				{
					cmdCnt++;
				}
			}
			customseconds.command = new string[cmdCnt];

			var index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Commands[i]))
				{
					customseconds.command[index++] = cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Commands[i];
				}
			}


			var customminutes = new JsonSettingsCustomMinutes()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled,
				entries = []
			};

			cmdCnt = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomMins.Commands[i]))
				{
					cmdCnt++;
				}
			}

			if (cmdCnt > 0)
			{
				customminutes.entries = new JsonCustomMinutes[cmdCnt];

				index = 0;
				for (var i = 0; i < 10; i++)
				{
					customminutes.entries[index] = new JsonCustomMinutes();

					if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomMins.Commands[i]))
					{
						customminutes.entries[index].command = cumulus.MySqlFuncs.MySqlSettings.CustomMins.Commands[i];
						customminutes.entries[index].intervalidx = cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i];
						customminutes.entries[index].catchup = cumulus.MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i];
						index++;

						if (index == cmdCnt)
						{
							break;
						}
					}
				}
			}
			var customrollover = new JsonSettingsCustomRolloverStart()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Enabled
			};

			cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Commands[i]))
				{
					cmdCnt++;
				}
			}
			customrollover.command = new string[cmdCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Commands[i]))
				{
					customrollover.command[index++] = cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Commands[i];
				}
			}

			var customtimed = new JsonSettingsCustomTimed()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Enabled,
				entries = []
			};

			cmdCnt = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]))
				{
					cmdCnt++;
				}
			}
			if (cmdCnt > 0)
			{
				customtimed.entries = new JsonCustomTimed[cmdCnt];

				index = 0;
				for (var i = 0; i < 10; i++)
				{
					customtimed.entries[index] = new JsonCustomTimed();

					if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]))
					{
						customtimed.entries[index].command = cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Commands[i];
						customtimed.entries[index].starttime = cumulus.MySqlFuncs.MySqlSettings.CustomTimed.StartTimes[i];
						customtimed.entries[index].interval = cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] == 1440 ? -1 : cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i];
						customtimed.entries[index].repeat = cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] != 1440;
						index++;

						if (index == cmdCnt)
						{
							break;
						}
					}
				}
			}

			var customstartup = new JsonSettingsCustomRolloverStart()
			{
				enabled = cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Enabled
			};

			cmdCnt = 1;
			for (var i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]))
				{
					cmdCnt++;
				}
			}
			customstartup.command = new string[cmdCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]))
				{
					customstartup.command[index++] = cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i];
				}
			}

			var options = new JsonSettingsOptions()
			{
				updateonedit = cumulus.MySqlFuncs.MySqlSettings.UpdateOnEdit,
				bufferonerror = cumulus.MySqlFuncs.MySqlSettings.BufferOnfailure,
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
			var json = string.Empty;
			JsonSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing MySQL Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("MySQL Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating MySQL settings");

				var connectRequired = false;

				// first check if any of the connection settings have changed
				// if they have then disconnect any existing connection so it can be reconnected if required
				if ((string.IsNullOrWhiteSpace(settings.server.host) ||
					cumulus.MySqlFuncs.MySqlConnSettings.Server != settings.server.host.Trim() ||
					cumulus.MySqlFuncs.MySqlConnSettings.Port != settings.server.port ||
					cumulus.MySqlFuncs.MySqlConnSettings.Database != settings.server.database.Trim() ||
					string.IsNullOrWhiteSpace(settings.server.user) ||
					cumulus.MySqlFuncs.MySqlConnSettings.UserID != settings.server.user.Trim() ||
					string.IsNullOrWhiteSpace(settings.server.pass) ||
					cumulus.MySqlFuncs.MySqlConnSettings.Password != settings.server.pass.Trim() ||
					cumulus.MySqlFuncs.MySqlConnSettings.SslMode != (MySqlSslMode) settings.server.advanced.sslMode ||
					cumulus.MySqlFuncs.MySqlConnSettings.TlsVersion != (string.IsNullOrWhiteSpace(settings.server.advanced.tlsVers) ? "TLS 1.2, TLS 1.3" : settings.server.advanced.tlsVers.Trim()))
					)
				{
					connectRequired = true;
				}

				// save the server settings

				cumulus.MySqlFuncs.MySqlConnSettings.Server = string.IsNullOrWhiteSpace(settings.server.host) ? null : settings.server.host.Trim();
				if (settings.server.port > 0 && settings.server.port < 65536)
				{
					cumulus.MySqlFuncs.MySqlConnSettings.Port = settings.server.port;
				}
				else
				{
					cumulus.MySqlFuncs.MySqlConnSettings.Port = 3306;
				}
				cumulus.MySqlFuncs.MySqlConnSettings.Database = string.IsNullOrWhiteSpace(settings.server.database) ? null : settings.server.database.Trim();
				cumulus.MySqlFuncs.MySqlConnSettings.UserID = string.IsNullOrWhiteSpace(settings.server.user) ? null : settings.server.user.Trim();
				cumulus.MySqlFuncs.MySqlConnSettings.Password = string.IsNullOrWhiteSpace(settings.server.pass) ? null : settings.server.pass.Trim();
				cumulus.MySqlFuncs.MySqlConnSettings.SslMode = (MySqlSslMode) settings.server.advanced.sslMode;
				cumulus.MySqlFuncs.MySqlConnSettings.TlsVersion = string.IsNullOrWhiteSpace(settings.server.advanced.tlsVers) ? "TLS 1.2, TLS 1.3" : settings.server.advanced.tlsVers.Trim();

				if (connectRequired)
				{

					if (!cumulus.MySqlFuncs.MySqlTestConnection().Result)
					{
						cumulus.LogMessage("MySqlSettings: Error connecting to server");
					}
				}

				// options
				cumulus.MySqlFuncs.MySqlSettings.UpdateOnEdit = settings.options.updateonedit;
				cumulus.MySqlFuncs.MySqlSettings.BufferOnfailure = settings.options.bufferonerror;

				//monthly
				cumulus.MySqlFuncs.MySqlSettings.Monthly.Enabled = settings.monthly.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.Monthly.Enabled)
				{
					cumulus.MySqlFuncs.MySqlSettings.Monthly.TableName = string.IsNullOrWhiteSpace(settings.monthly.table) ? "Monthly" : settings.monthly.table.Trim();
					if (cumulus.MySqlFuncs.MySqlSettings.Monthly.TableName != cumulus.MonthlyTable.Name)
					{
						cumulus.MonthlyTable.Name = cumulus.MySqlFuncs.MySqlSettings.Monthly.TableName;
						cumulus.MonthlyTable.Rebuild();
					}
				}
				//realtime
				cumulus.MySqlFuncs.MySqlSettings.Realtime.Enabled = settings.realtime.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.Realtime.Enabled)
				{
					cumulus.MySqlFuncs.MySqlSettings.RealtimeRetention = settings.realtime.retentionVal + " " + settings.realtime.retentionUnit.Trim();
					cumulus.MySqlFuncs.MySqlSettings.Realtime.TableName = string.IsNullOrWhiteSpace(settings.realtime.table) ? "Realtime" : settings.realtime.table.Trim();
					cumulus.MySqlFuncs.MySqlSettings.RealtimeLimit1Minute = settings.realtime.limit1min;
					if (cumulus.MySqlFuncs.MySqlSettings.Realtime.TableName != cumulus.RealtimeTable.Name)
					{
						cumulus.RealtimeTable.Name = cumulus.MySqlFuncs.MySqlSettings.Realtime.TableName;
						cumulus.RealtimeTable.Rebuild();
					}
				}
				//dayfile
				cumulus.MySqlFuncs.MySqlSettings.Dayfile.Enabled = settings.dayfile.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.Dayfile.Enabled)
				{
					cumulus.MySqlFuncs.MySqlSettings.Dayfile.TableName = string.IsNullOrWhiteSpace(settings.dayfile.table) ? "Dayfile" : settings.dayfile.table.Trim();
					if (cumulus.MySqlFuncs.MySqlSettings.Dayfile.TableName != cumulus.DayfileTable.Name)
					{
						cumulus.DayfileTable.Name = cumulus.MySqlFuncs.MySqlSettings.Dayfile.TableName;
						cumulus.DayfileTable.Rebuild();
					}
				}
				// custom seconds
				cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Enabled = settings.customseconds.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (settings.customseconds.command != null && i < settings.customseconds.command.Length)
							cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Commands[i] = string.IsNullOrWhiteSpace(settings.customseconds.command[i]) ? null : settings.customseconds.command[i].Trim();
						else
							cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Commands[i] = null;
					}

					cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Interval = settings.customseconds.interval;
				}
				// custom minutes
				cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled = settings.customminutes.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customminutes.entries.Length)
						{
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.Commands[i] = string.IsNullOrWhiteSpace(settings.customminutes.entries[i].command) ? null : settings.customminutes.entries[i].command.Trim();
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] = settings.customminutes.entries[i].intervalidx;
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i] = settings.customminutes.entries[i].catchup;
							if (cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] >= 0 && cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] < Cumulus.FactorsOf60.Length)
							{
								cumulus.MySqlFuncs.MySqlSettings.CustomMins.Intervals[i] = Cumulus.FactorsOf60[cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i]];
							}
							else
							{
								cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] = 6;
								cumulus.MySqlFuncs.MySqlSettings.CustomMins.Intervals[i] = 10;
							}
						}
						else
						{
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.Commands[i] = null;
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] = 6;
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.Intervals[i] = 10;
							cumulus.MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i] = false;
						}
					}
				}
				// custom roll-over
				cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Enabled = settings.customrollover.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (settings.customrollover.command != null && i < settings.customrollover.command.Length)
							cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Commands[i] = string.IsNullOrWhiteSpace(settings.customrollover.command[i]) ? null : settings.customrollover.command[i].Trim();
						else
							cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Commands[i] = null;
					}
				}
				// custom timed
				cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Enabled = settings.customtimed.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Enabled && null != settings.customtimed.entries)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customtimed.entries.Length)
						{
							cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Commands[i] = string.IsNullOrWhiteSpace(settings.customtimed.entries[i].command) ? null : settings.customtimed.entries[i].command.Trim();
							cumulus.MySqlFuncs.MySqlSettings.CustomTimed.StartTimes[i] = settings.customtimed.entries[i].starttime;
							if (settings.customtimed.entries[i].repeat)
							{
								cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] = settings.customtimed.entries[i].interval == -1 ? 1440 : settings.customtimed.entries[i].interval;
								cumulus.MySqlFuncs.MySqlSettings.CustomTimed.SetNextInterval(i, DateTime.Now);
							}
							else
							{
								cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] = 1440;
							}
						}
						else
						{
							cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Commands[i] = null;
							cumulus.MySqlFuncs.MySqlSettings.CustomTimed.StartTimes[i] = TimeSpan.Zero;
							cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] = 1440;
						}
					}
				}
				// custom start-up
				cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Enabled = settings.customstart.enabled;
				if (cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Enabled)
				{
					for (var i = 0; i < 10; i++)
					{
						if (i < settings.customstart.command.Length)
							cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i] = string.IsNullOrWhiteSpace(settings.customstart.command[i]) ? null : settings.customstart.command[i].Trim();
						else
							cumulus.MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i] = null;
					}
				}

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				var msg = "Error processing settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				context.Response.StatusCode = 500;
				return msg;
			}
			return "success";
		}

		public string CreateMonthlySQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.CreateMySQLTable(cumulus.MonthlyTable.CreateCommand) + "\"}";
		}

		public string CreateDayfileSQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.CreateMySQLTable(cumulus.DayfileTable.CreateCommand) + "\"}";
		}

		public string CreateRealtimeSQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.CreateMySQLTable(cumulus.RealtimeTable.CreateCommand) + "\"}";
		}

		public string UpdateMonthlySQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.UpdateMySQLTable(cumulus.MonthlyTable) + "\"}";
		}

		public string UpdateDayfileSQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.UpdateMySQLTable(cumulus.DayfileTable) + "\"}";
		}

		public string UpdateRealtimeSQL()
		{
			return "{\"result\":\"" + cumulus.MySqlFuncs.UpdateMySQLTable(cumulus.RealtimeTable) + "\"}";
		}

		private sealed class JsonSettings
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

		private sealed class JsonSettingsServer
		{
			public string host { get; set; }
			public uint port { get; set; }
			public string user { get; set; }
			public string pass { get; set; }
			public string database { get; set; }
			public SettingsServerAdvanced advanced { get; set; }
		}

		private sealed class SettingsServerAdvanced
		{
			public uint sslMode { get; set; }
			public string tlsVers { get; set; }
		}

		private sealed class JsonSettingsOptions
		{
			public bool updateonedit { get; set; }
			public bool bufferonerror { get; set; }
		}

		private sealed class JsonSettingsMonthly
		{
			public bool enabled { get; set; }
			public string table { get; set; }
		}

		private sealed class JsonSettingsRealtime
		{
			public bool enabled { get; set; }
			public string table { get; set; }
			public int retentionVal { get; set; }
			public string retentionUnit { get; set; }
			public bool limit1min { get; set; }
		}

		private sealed class JsonSettingsDayfile
		{
			public bool enabled { get; set; }
			public string table { get; set; }
		}

		private sealed class JsonSettingsCustomSeconds
		{
			public bool enabled { get; set; }
			public string[] command { get; set; }
			public int interval { get; set; }
		}

		private sealed class JsonSettingsCustomMinutes
		{
			public bool enabled { get; set; }
			public JsonCustomMinutes[] entries { get; set; }
		}

		private sealed class JsonCustomMinutes
		{
			public string command { get; set; }
			public int intervalidx { get; set; }
			public bool catchup { get; set; }
		}

		private sealed class JsonSettingsCustomRolloverStart
		{
			public bool enabled { get; set; }
			public string[] command { get; set; }
		}

		private sealed class JsonSettingsCustomTimed
		{
			public bool enabled { get; set; }
			public JsonCustomTimed[] entries { get; set; }
		}

		private sealed class JsonCustomTimed
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
