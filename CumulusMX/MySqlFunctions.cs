using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySqlConnector;

namespace CumulusMX
{
	internal class MySqlFunctions
	{
		internal MySqlConnectionStringBuilder MySqlConnSettings = [];

		internal MySqlGeneralSettings MySqlSettings = new();

		private static readonly Cumulus cumulus = Program.cumulus;
		private static readonly WeatherStation station = cumulus.Station;

		// Use thread safe queues for the MySQL command lists
		internal readonly ConcurrentQueue<SqlCache> MySqlList = new();
		internal readonly ConcurrentQueue<SqlCache> MySqlFailedList = new();


		internal async Task MySqlCommandAsync(string Cmd, string CallingFunction)
		{
			await CheckMySQLFailedUploads(CallingFunction, Cmd);
		}

		internal async Task MySqlCommandAsync(List<string> Cmds, string CallingFunction)
		{
			await CheckMySQLFailedUploads(CallingFunction, Cmds);
		}

		private async Task CheckMySQLFailedUploads(string callingFunction, string cmd)
		{
			await CheckMySQLFailedUploads(callingFunction, [cmd]);
		}

		private async Task CheckMySQLFailedUploads(string callingFunction, List<string> cmds)
		{
			try
			{
				if (!MySqlFailedList.IsEmpty)
				{
					// flag we are processing the queue so the next task doesn't try as well
					cumulus.SqlCatchingUp = true;

					cumulus.LogMessage($"{callingFunction}: Failed MySQL updates are present");

					try
					{
						Thread.Sleep(500);
						cumulus.LogMessage($"{callingFunction}: Connection to MySQL server is OK, trying to upload {MySqlFailedList.Count} failed commands");

						await ProcessMySqlBuffer(MySqlFailedList, callingFunction, true);
						cumulus.LogMessage($"{callingFunction}: Upload of failed MySQL commands complete");
					}
					catch
					{
						if (MySqlSettings.BufferOnfailure)
						{
							cumulus.LogMessage($"{callingFunction}: Connection to MySQL server has failed, adding this update to the failed list");
							if (callingFunction.StartsWith("Realtime["))
							{
								var tmp = new SqlCache() { statement = cmds[0] };
								_ = station.RecentDataDb.Insert(tmp);

								// don't bother buffering the realtime deletes - if present
								MySqlFailedList.Enqueue(tmp);
							}
							else
							{
								for (var i = 0; i < cmds.Count; i++)
								{
									var tmp = new SqlCache() { statement = cmds[i] };

									_ = station.RecentDataDb.Insert(tmp);

									MySqlFailedList.Enqueue(tmp);
								}
							}
						}
					}

					cumulus.SqlCatchingUp = false;
				}

				// now do what we came here to do
				if (cmds.Count > 0)
				{
					await ProcessMySqlBuffer(cmds, callingFunction, false);
				}
				else if (cmds.Count == 0)
				{
					cumulus.LogDebugMessage($"{callingFunction}: No SQL cmds found to process!");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, $"{callingFunction}: Error during MySQL upload");
				cumulus.SqlCatchingUp = false;
			}
		}

		internal async Task ProcessMySqlStartupBuffer()
		{
			cumulus.LogMessage($"Starting MySQL catchup thread. Found {MySqlList.Count} commands to execute");

			await ProcessMySqlBuffer(MySqlList, "Startup MySQL", false);
		}

		private async Task ProcessMySqlBuffer(List<string> Cmds, string CallingFunction, bool UsingFailedList)
		{
			var tempQ = new ConcurrentQueue<SqlCache>();
			foreach (var cmd in Cmds)
			{
				tempQ.Enqueue(new SqlCache() { statement = cmd });
			}

			await ProcessMySqlBuffer(tempQ, CallingFunction, UsingFailedList);
		}

		private async Task ProcessMySqlBuffer(ConcurrentQueue<SqlCache> myQueue, string CallingFunction, bool UsingFailedList)
		{
			try
			{
				await using var MySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
				await MySqlConn.OpenAsync();

				using var transaction = myQueue.Count > 2 ? await MySqlConn.BeginTransactionAsync() : null;

				SqlCache cachedCmd;
				// Do not remove the item from the stack until we know the command worked, or the command is bad
				while (myQueue.TryPeek(out cachedCmd))
				{
					try
					{
						if (string.IsNullOrEmpty(cachedCmd.statement))
						{
							// remove empty cmd from the queue
							myQueue.TryDequeue(out _);
						}
						else
						{
							using (MySqlCommand cmd = new MySqlCommand(cachedCmd.statement, MySqlConn))
							{
								cumulus.LogDebugMessage($"{CallingFunction}: MySQL executing - {cachedCmd.statement}");

								if (transaction != null)
								{
									cmd.Transaction = transaction;
								}

								int aff = await cmd.ExecuteNonQueryAsync();
								cumulus.LogDebugMessage($"{CallingFunction}: MySQL {aff} rows were affected.");

								// Success, if using the failed list, delete from the databasec
								if (UsingFailedList)
								{
									station.RecentDataDb.Delete<SqlCache>(cachedCmd.key);
								}
								// and pop the value from the queue
								myQueue.TryDequeue(out _);
							}

							cumulus.MySqlUploadAlarm.Triggered = false;
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{CallingFunction}: Error encountered during MySQL operation = {ex.Message}");

						// if debug logging is disable, then log the failing statement anyway
						if (!cumulus.DebuggingEnabled)
						{
							cumulus.LogMessage($"{CallingFunction}: Failing SQL = {cachedCmd.statement}");
						}

						cumulus.MySqlUploadAlarm.LastMessage = ex.Message;
						cumulus.MySqlUploadAlarm.Triggered = true;

						var errorCode = (int) ex.Data["Server Error Code"];

						// do we save this command/commands on failure to be resubmitted?
						// if we have a syntax error, it is never going to work so do not save it for retry
						if (MySqlSettings.BufferOnfailure && !UsingFailedList)
						{
							// do we save this command/commands on failure to be resubmitted?
							// if we have a syntax error, it is never going to work so do not save it for retry
							// A selection of the more common(?) errors to ignore...
							MySqlCommandErrorHandler(CallingFunction, errorCode, myQueue);
						}
						else if (UsingFailedList)
						{
							// We are processing the buffered list
							if (MySqlCheckError(errorCode))
							{
								// there is something wrong with the command, discard it
								cumulus.LogMessage($"{CallingFunction}: Discarding bad SQL = {cachedCmd.statement}. Error = \"{MySqlErrorToText(errorCode)}\"");
								myQueue.TryDequeue(out _);
								station.RecentDataDb.Delete<SqlCache>(cachedCmd.key);
							}
							else
							{
								// something else went wrong - abort
								cumulus.LogExceptionMessage(ex, $"{CallingFunction}: Something went wrong processing MySQL buffer");
								break;
							}
						}
					}
				}

				if (transaction != null)
				{
					cumulus.LogDebugMessage($"{CallingFunction}: Committing updates to DB");
					try
					{
						await transaction.CommitAsync();
						cumulus.LogDebugMessage($"{CallingFunction}: Commit complete");
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"{CallingFunction}: Error committing transaction");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"{CallingFunction}: General failure! Error = {ex.Message}");

				// do we want to add the commands to the failed buffer?
				if (!UsingFailedList)
				{
					foreach (var item in myQueue)
					{
						MySqlFailedList.Enqueue(item);
					}
				}

				cumulus.MySqlUploadAlarm.LastMessage = ex.Message;
				cumulus.MySqlUploadAlarm.Triggered = true;
				return;
			}

			cumulus.MySqlUploadAlarm.Triggered = false;
		}

		private static bool MySqlCheckError(int ErrorCode)
		{
			return ErrorCode == (int) MySqlErrorCode.ParseError ||
				   ErrorCode == (int) MySqlErrorCode.EmptyQuery ||
				   ErrorCode == (int) MySqlErrorCode.TooBigSelect ||
				   ErrorCode == (int) MySqlErrorCode.InvalidUseOfNull ||
				   ErrorCode == (int) MySqlErrorCode.MixOfGroupFunctionAndFields ||
				   ErrorCode == (int) MySqlErrorCode.SyntaxError ||
				   ErrorCode == (int) MySqlErrorCode.TooLongString ||
				   ErrorCode == (int) MySqlErrorCode.WrongColumnName ||
				   ErrorCode == (int) MySqlErrorCode.DuplicateUnique ||
				   ErrorCode == (int) MySqlErrorCode.PrimaryCannotHaveNull ||
				   ErrorCode == (int) MySqlErrorCode.DivisionByZero ||
				   ErrorCode == (int) MySqlErrorCode.DuplicateKeyEntry;

		}

		private static string MySqlErrorToText(int ErrorCode)
		{
			return (MySqlErrorCode) ErrorCode switch
			{
				MySqlErrorCode.ParseError => "Parsing error",
				MySqlErrorCode.EmptyQuery => "Empty query",
				MySqlErrorCode.TooBigSelect => "Select statement too big",
				MySqlErrorCode.InvalidUseOfNull => "Invalid use of null",
				MySqlErrorCode.MixOfGroupFunctionAndFields => "Mixed group of functions and fields",
				MySqlErrorCode.SyntaxError => "Syntax error",
				MySqlErrorCode.TooLongString => "Too long string in query",
				MySqlErrorCode.WrongColumnName => "Wrong column name used",
				MySqlErrorCode.DuplicateUnique => "Attempt to create a duplicate unique entry",
				MySqlErrorCode.PrimaryCannotHaveNull => "Primary column cannot be null",
				MySqlErrorCode.DivisionByZero => "Division by zero",
				MySqlErrorCode.DuplicateKeyEntry => "Duplicate key entry",
				_ => "Unknown error code " + ErrorCode,
			};
		}

		private void MySqlCommandErrorHandler(string CallingFunction, int ErrorCode, ConcurrentQueue<SqlCache> Cmds)
		{
			var ignore = MySqlCheckError(ErrorCode);

			if (ignore)
			{
				cumulus.LogDebugMessage($"{CallingFunction}: Not buffering this command due to a problem with the query. Error = " + MySqlErrorToText(ErrorCode));
			}
			else
			{
				while (!Cmds.IsEmpty)
				{
					try
					{
						Cmds.TryDequeue(out var cmd);
						if (!cmd.statement.StartsWith("DELETE"))
						{
							cumulus.LogDebugMessage($"{CallingFunction}: Buffering command to failed list");

							_ = station.RecentDataDb.Insert(cmd);
							MySqlFailedList.Enqueue(cmd);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{CallingFunction}: Error buffering command - " + ex.Message);
					}
				}
			}
		}

		internal async Task<bool> MySqlTestConnection()
		{
			cumulus.LogMessage("MySqlTestConnection: Starting Connection test");

			try
			{
				cumulus.LogMessage($"MySqlTestConnection: Connecting to server {MySqlConnSettings.Server} port {MySqlConnSettings.Port}");

				await using var MySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
				await MySqlConn.OpenAsync();

				cumulus.LogMessage("MySqlTestConnection: Connection opened OK");

				if (await MySqlConn.PingAsync())
				{
					cumulus.LogMessage("MySqlTestConnection: Server Ping OK");
					return true;
				}
				else
				{
					cumulus.LogMessage("MySqlTestConnection: Server Ping failed");
					return false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "MySqlTestConnection: Error creating the connection");
				return false;
			}
		}

		/*
		internal async Task<bool> CheckConnection()
		{
			if (MySqlConn is null || MySqlConn.State == System.Data.ConnectionState.Closed)
			{
				// force a disconnect/reconnect
				return await MySqlConnect();
			}

			try
			{
					return await MySqlConn.PingAsync();
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "MySqlCheckConnection: failed to Ping the database");

				// force a disconnect/reconnect
				return await MySqlConnect();
			}
		}
*/
		internal string GetCachedSqlCommands(string draw, int start, int length, string search)
		{
			try
			{
				var filtered = 0;
				var thisDraw = 0;


				var json = new StringBuilder(350 * MySqlFailedList.Count);

				json.Append("{\"data\":[");

				foreach (var rec in MySqlFailedList)
				{
					// if we have a search string and no match, skip to next line
					if (!string.IsNullOrEmpty(search) && !rec.statement.Contains(search))
					{
						continue;
					}

					// this line either matches the search
					filtered++;

					// skip records until we get to the start entry
					if (filtered <= start)
					{
						continue;
					}

					// only send the number requested
					if (thisDraw < length)
					{
						// track the number of lines we have to return so far
						thisDraw++;

						json.Append($"[{rec.key},\"{rec.statement}\"],");
					}
					else if (string.IsNullOrEmpty(search))
					{
						// no search so we can bail out as we already know the total number of records
						break;
					}
				}

				// trim last ","
				if (thisDraw > 0)
					json.Length--;
				json.Append("],\"recordsTotal\":");
				json.Append(MySqlFailedList.Count);
				json.Append(",\"draw\":");
				json.Append(draw);
				json.Append(",\"recordsFiltered\":");
				json.Append(string.IsNullOrEmpty(search) ? MySqlFailedList.Count : filtered);
				json.Append('}');

				return json.ToString();

			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetCachedSqlCommands: Error - " + ex.ToString());
			}

			return "";
		}

		internal string CreateMySQLTable(string createSQL)
		{
			string res;

			using var MySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
			MySqlConn.Open();

			using (var cmd = new MySqlCommand(createSQL, MySqlConn))
			{
				cumulus.LogMessage($"CreateMySQLTable: {createSQL}");

				try
				{
					var aff = cmd.ExecuteNonQuery();
					cumulus.LogMessage($"CreateMySQLTable: {aff} items were affected.");
					res = "Database table created successfully";
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("CreateMySQLTable: Error encountered during MySQL operation.");
					cumulus.LogMessage(ex.Message);
					res = "Error: " + ex.Message;
				}
			}

			return res;
		}

		internal string UpdateMySQLTable(MySqlTable table)
		{
			string res;
			var cnt = 0;

			// No locking - just do it

			try
			{
				using var MySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
				MySqlConn.Open();

				// first get a list of the columns the table currenty has
				var currCols = new List<string>();
				using (var cmd = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{table.Name}' AND TABLE_SCHEMA='{MySqlConnSettings.Database}'", MySqlConn))
				using (var reader = cmd.ExecuteReader())
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

					using var cmd = new MySqlCommand(update.ToString(), MySqlConn);
					_ = cmd.ExecuteNonQuery();
					res = $"Added {cnt} columns to {table.Name} table";
					cumulus.LogMessage($"UpdateMySQLTable: " + res);
				}
				else
				{
					res = $"The {table.Name} table already has all the required columns. Required = {table.Columns.Count}, actual = {currCols.Count}";
					cumulus.LogMessage("UpdateMySQLTable: " + res);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("UpdateMySQLTable: Error encountered during MySQL operation.");
				cumulus.LogErrorMessage(ex.Message);
				res = "Error: " + ex.Message;
			}

			return res;
		}
	}
}
