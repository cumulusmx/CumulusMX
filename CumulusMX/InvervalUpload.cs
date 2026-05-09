using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FluentFTP;
using FluentFTP.Logging;

using Renci.SshNet;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public async Task DoIntervalUpload(byte cycle)
		{
			var remotePath = string.Empty;

			if (!FtpOptions.Enabled || !FtpOptions.IntervalEnabled)
				return;

			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = FtpOptions.Directory.EndsWith('/') ? FtpOptions.Directory : FtpOptions.Directory + '/';
			}


			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				await UploadSFTP(cycle, remotePath);
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				await UploadFTP(cycle, remotePath);
			}
			else if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				await UploadPHP(cycle, remotePath);
			}
		}

		private async Task UploadSFTP(byte cycle, string remotePath)
		{
			var msgPrefix = $"Interval[{cycle}] SFTP:";
			var cycle1k = 1000 + cycle;

			LogDebugMessage(msgPrefix + " Process starting");
			try
			{
				using SftpClient conn = await sftpClientFactory.CreateClient();
				try
				{
					LogDebugMessage($"{msgPrefix} CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}");
					conn.Connect();
				}
				catch (Exception ex)
				{
					LogErrorMessage($"{msgPrefix} Error connecting SFTP - {ex.Message}");

					FtpAlarm.LastMessage = "Error connecting SFTP - " + ex.Message;
					FtpAlarm.Triggered = true;
					return;
				}

				if (conn.IsConnected)
				{
					LogDebugMessage($"{msgPrefix} CumulusMX Connected to {FtpOptions.Hostname} OK");

					if (NOAAconf.NeedFtp)
					{
						var success = false;
						try
						{
							// upload NOAA reports
							LogDebugMessage($"{msgPrefix} Uploading NOAA reports");

							var uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestMonthReport);
							var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

							success = UploadFile(conn, uploadfile, remotefile, cycle1k);

							uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestYearReport);
							remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

							success = success && UploadFile(conn, uploadfile, remotefile, cycle1k);

							LogDebugMessage($"{msgPrefix} Done uploading NOAA reports");
						}
						catch (Exception e)
						{
							LogErrorMessage($"{msgPrefix} Error uploading file - {e.Message}");
							FtpAlarm.LastMessage = "Error uploading NOAA report file - " + e.Message;
							FtpAlarm.Triggered = true;
						}
						NOAAconf.NeedFtp = !success;
					}

					// Extra files
					var eodSuccess = true;
					for (var i = 0; i < ActiveExtraFiles.Count; i++)
					{
						var item = ActiveExtraFiles[i];

						if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
						{
							continue;
						}

						// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
						var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;
						var uploadfile = GetUploadFilename(item.local, logDay);

						if (!File.Exists(uploadfile))
						{
							LogWarningMessage($"{msgPrefix}: Extra web file [{uploadfile}] not found!");
							FtpAlarm.LastMessage = $"Error Extra web file [{uploadfile} not found";
							FtpAlarm.Triggered = true;
							continue;
						}

						var remotefile = GetRemoteFileName(item.remote, logDay);

						LogDebugMessage($"{msgPrefix} Uploading Extra web file: {uploadfile}");

						// all checks OK, file needs to be uploaded
						try
						{
							// Is this an incremental log file upload?
							if (item.incrementalLogfile && !item.binary)
							{
								// has the log file rolled over?
								if (item.logFileLastFileName != uploadfile)
								{
									ActiveExtraFiles[i].logFileLastFileName = uploadfile;
									ActiveExtraFiles[i].logFileLastLineNumber = 0;
								}

								var linesAdded = 0;
								var data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

								if (linesAdded == 0)
								{
									LogDebugMessage($"{msgPrefix} Extra web file: {uploadfile} - No incremental data found, skipping this upload");
									continue;
								}

								// have we already uploaded the base file?
								if (item.logFileLastLineNumber > 0)
								{
									if (AppendText(conn, remotefile, data, -1, linesAdded))
									{
										ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
									}
								}
								else // no, just upload the base file
								{
									if (UploadFile(conn, uploadfile, remotefile, -1))
									{
										ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
									}
								}
							}
							else if (item.process)
							{
								LogDebugMessage($"{msgPrefix} Processing Extra web file: {uploadfile}");
								var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
								using var strm = GenerateStreamFromString(data);
								eodSuccess = eodSuccess && UploadStream(conn, remotefile, strm, cycle1k);
							}
							else
							{
								eodSuccess = eodSuccess && UploadFile(conn, uploadfile, remotefile, cycle1k);
							}
						}
						catch (Exception e)
						{
							LogErrorMessage($"{msgPrefix} Error uploading Extra web file [{uploadfile}]");
							LogMessage($"{msgPrefix} Error = {e.Message}");
							FtpAlarm.LastMessage = $"Error uploading Extra web file [{uploadfile}";
							FtpAlarm.Triggered = true;
						}
					}

					if (EODfilesNeedFTP)
					{
						EODfilesNeedFTP = !eodSuccess;
					}

					// standard files
					for (var i = 0; i < StdWebFiles.Length; i++)
					{
						if (StdWebFiles[i].FTP)
						{
							try
							{
								var localFile = StdWebFiles[i].LocalPath + StdWebFiles[i].FileName;
								var remotefile = remotePath + StdWebFiles[i].FileName;
								LogDebugMessage($"{msgPrefix} Uploading standard Data file: {localFile}");

								string data;

								if (StdWebFiles[i].FileName == "wxnow.txt")
								{
									data = station.CreateWxnowFileString();
								}
								else
								{
									data = await ProcessTemplateFile2StringAsync(StdWebFiles[i].TemplateFileName, true, true);
								}

								using var dataStream = GenerateStreamFromString(data);
								UploadStream(conn, remotefile, dataStream, cycle1k);
							}
							catch (Exception e)
							{
								LogErrorMessage($"{msgPrefix} Error uploading standard data file [{StdWebFiles[i].FileName}]");
								LogMessage($"{msgPrefix} Error = {e}");
								FtpAlarm.LastMessage = $"Error uploading standard web file {StdWebFiles[i].FileName} - {e.Message}";
								FtpAlarm.Triggered = true;
							}
						}
					}

					for (int i = 0; i < GraphDataFiles.Length; i++)
					{
						if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
						{
							var uploadfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].FileName;
							var remotefile = remotePath + GraphDataFiles[i].FileName;

							try
							{
								LogDebugMessage($"{msgPrefix} Uploading graph data file: {uploadfile}");

								var json = station.CreateGraphDataJson(GraphDataFiles[i].FileName, false);

								using var dataStream = GenerateStreamFromString(json);
								if (UploadStream(conn, remotefile, dataStream, cycle1k))
								{
									// Uploaded OK, reset the upload required flag for the static and daily files
									if (i == (int) GraphFileIdx.CONFIG || i == (int) GraphFileIdx.AVAILABLE || i == (int) GraphFileIdx.DAILYRAIN || i == (int) GraphFileIdx.DAILYTEMP || i == (int) GraphFileIdx.SUNHOURS)
									{
										GraphDataFiles[i].FtpRequired = false;
									}
								}
							}
							catch (Exception e)
							{
								LogErrorMessage($"{msgPrefix} Error uploading graph data file [{uploadfile}]");
								LogMessage($"{msgPrefix} Error = {e}");
								FtpAlarm.LastMessage = $"Error uploading graph data file [{uploadfile}] - {e.Message}";
								FtpAlarm.Triggered = true;
							}
						}
					}

					for (int i = 0; i < GraphDataEodFiles.Length; i++)
					{
						if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
						{
							var uploadfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].FileName;
							var remotefile = remotePath + GraphDataEodFiles[i].FileName;
							try
							{
								LogDebugMessage($"{msgPrefix} Uploading daily graph data file: {uploadfile}");

								var json = station.CreateEodGraphDataJson(GraphDataEodFiles[i].FileName);

								using var dataStream = GenerateStreamFromString(json);
								if (UploadStream(conn, remotefile, dataStream, cycle1k))
								{
									// Uploaded OK, reset the upload required flag
									GraphDataEodFiles[i].FtpRequired = false;
								}
							}
							catch (Exception e)
							{
								LogErrorMessage($"{msgPrefix} Error uploading daily graph data file [{uploadfile}]");
								LogMessage($"{msgPrefix} Error = {e}");
								FtpAlarm.LastMessage = $"Error uploading daily graph data file [{uploadfile}] - {e.Message}";
								FtpAlarm.Triggered = true;
							}
						}
					}

					if (MoonImage.Ftp && MoonImage.ReadyToFtp)
					{
						try
						{
							LogDebugMessage($"{msgPrefix} Uploading Moon image file");
							if (UploadFile(conn, Path.Combine("web", "moon.png"), remotePath + MoonImage.FtpDest, -1))
							{
								// clear the image ready for FTP flag, only upload once an hour
								MoonImage.ReadyToFtp = false;
							}
							LogDebugMessage("SFTP[Int]: Done uploading Moon image file");
						}
						catch (Exception e)
						{
							LogErrorMessage($"{msgPrefix} Error uploading moon image - {e.Message}");
							FtpAlarm.LastMessage = $"Error uploading moon image - {e.Message}";
							FtpAlarm.Triggered = true;
						}
					}
				}

				try
				{
					// do not error on disconnect
					conn.Disconnect();
				}
				catch
				{
					// do nothing
				}
			}
			catch (Exception ex)
			{
				LogErrorMessage($"{msgPrefix} Error using SFTP connection - {ex.Message}");
			}
			LogDebugMessage($"{msgPrefix} Process complete");
		}

		private async Task UploadFTP(byte cycle, string remotePath)
		{
			var msgPrefix = $"Interval[{cycle}] FTP:";
			var cycle1k = 1000 + cycle;

			using FtpClient conn = await ftpClientFactory.CreateClient();

			if (FtpOptions.Logging)
			{
				conn.Logger = new FtpLogAdapter(FtpLoggerIN);
			}

			LogFtpMessage("", false); // insert a blank line
			LogFtpDebugMessage($"{msgPrefix} CumulusMX Connecting to " + FtpOptions.Hostname, false);

			try
			{
				if (FtpOptions.AutoDetect)
				{
					conn.AutoConnect();
				}
				else
				{
					conn.Connect();
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"{msgPrefix} Error connecting ftp - {ex.Message}", false);

				FtpAlarm.LastMessage = "Error connecting ftp - " + ex.Message;
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogFtpMessage($"{msgPrefix} Base exception - {ex.Message}", false);
				}

				return;
			}

			if (conn.IsConnected)
			{
				if (NOAAconf.NeedFtp)
				{
					var success = false;
					try
					{
						// upload NOAA reports
						LogFtpMessage("", false);
						LogFtpDebugMessage($"{msgPrefix} Uploading NOAA reports", false);

						var uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestMonthReport);
						var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

						success = UploadFile(conn, uploadfile, remotefile, cycle);

						uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestYearReport);
						remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

						success = success && UploadFile(conn, uploadfile, remotefile, cycle);
						LogFtpDebugMessage($"{msgPrefix} Upload of NOAA reports complete", false);
					}
					catch (Exception e)
					{
						LogFtpMessage($"{msgPrefix} Error uploading NOAA files: {e.Message}", false);
						FtpAlarm.LastMessage = "Error connecting ftp - " + e.Message;
						FtpAlarm.Triggered = true;
					}
					NOAAconf.NeedFtp = !success;
				}

				// Extra files
				var eodSuccess = true;
				for (var i = 0; i < ActiveExtraFiles.Count; i++)
				{
					var item = ActiveExtraFiles[i];

					if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
					{
						continue;
					}

					// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
					var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;
					var uploadfile = GetUploadFilename(item.local, logDay);

					if (!File.Exists(uploadfile))
					{
						LogFtpMessage($"{msgPrefix} Extra web file [{uploadfile}] not found!", false);
						FtpAlarm.LastMessage = $"Error Extra web file [{uploadfile} not found";
						FtpAlarm.Triggered = true;
						continue;
					}

					var remotefile = GetRemoteFileName(item.remote, logDay);

					LogFtpMessage("", false);
					LogFtpDebugMessage($"{msgPrefix} Uploading Extra web file: {uploadfile}", false);

					// all checks OK, file needs to be uploaded

					try
					{
						// Is this an incremental log file upload?
						if (item.incrementalLogfile && !item.binary)
						{
							// has the log file rolled over?
							if (item.logFileLastFileName != uploadfile)
							{
								ActiveExtraFiles[i].logFileLastFileName = uploadfile;
								ActiveExtraFiles[i].logFileLastLineNumber = 0;
							}

							var linesAdded = 0;
							var data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

							if (linesAdded == 0)
							{
								LogDebugMessage($"{msgPrefix} Extra web file: {uploadfile} - No incremental data found, skipping this upload");
								continue;
							}

							// have we already uploaded the base file?
							if (item.logFileLastLineNumber > 0)
							{
								if (AppendText(conn, remotefile, data, -1, linesAdded))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
							else // no, just upload the base file
							{
								if (UploadFile(conn, uploadfile, remotefile, -1))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
						}
						else if (item.process)
						{
							LogFtpDebugMessage($"{msgPrefix} Processing Extra web file: " + uploadfile, false);
							var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
							using var strm = GenerateStreamFromString(data);
							eodSuccess = eodSuccess && UploadStream(conn, remotefile, strm, cycle1k);
						}
						else
						{
							eodSuccess = eodSuccess && UploadFile(conn, uploadfile, remotefile, cycle1k);
						}
					}
					catch (Exception e)
					{
						LogFtpMessage($"{msgPrefix} Error uploading file {uploadfile}: {e.Message}", false);
						FtpAlarm.LastMessage = $"Error uploading extra file {uploadfile} - {e.Message}";
						FtpAlarm.Triggered = true;
					}
				}

				if (EODfilesNeedFTP)
				{
					EODfilesNeedFTP = !eodSuccess;
				}

				// standard files
				for (int i = 0; i < StdWebFiles.Length; i++)
				{
					if (StdWebFiles[i].FTP)
					{
						try
						{
							var localfile = StdWebFiles[i].LocalPath + StdWebFiles[i].FileName;
							LogFtpDebugMessage($"{msgPrefix} Uploading standard Data file: {localfile}", false);

							string data;

							if (StdWebFiles[i].FileName == "wxnow.txt")
							{
								data = station.CreateWxnowFileString();
							}
							else
							{
								data = await ProcessTemplateFile2StringAsync(StdWebFiles[i].TemplateFileName, true, true);
							}

							using (var dataStream = GenerateStreamFromString(data))
							{
								UploadStream(conn, remotePath + StdWebFiles[i].FileName, dataStream, cycle1k);
							}

							// Uploaded OK, reset the upload required flag
							StdWebFiles[i].FtpRequired = false;
						}
						catch (Exception e)
						{
							LogFtpMessage($"{msgPrefix} Error uploading file {StdWebFiles[i].FileName}: {e}", false);
							FtpAlarm.LastMessage = $"Error uploading file {StdWebFiles[i].FileName} - {e.Message}";
							FtpAlarm.Triggered = true;
						}
					}
				}

				for (int i = 0; i < GraphDataFiles.Length; i++)
				{
					if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
					{
						try
						{
							var localfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].FileName;
							var remotefile = remotePath + GraphDataFiles[i].FileName;
							LogFtpDebugMessage($"{msgPrefix} Uploading graph data file: {localfile}", false);

							var json = station.CreateGraphDataJson(GraphDataFiles[i].FileName, false);

							using (var dataStream = GenerateStreamFromString(json))
							{
								UploadStream(conn, remotefile, dataStream, cycle1k);
							}

							// Uploaded OK, reset the upload required flag for files that only need a daily upload
							if (i == (int) GraphFileIdx.CONFIG || i == (int) GraphFileIdx.AVAILABLE || i == (int) GraphFileIdx.DAILYRAIN || i == (int) GraphFileIdx.DAILYTEMP || i == (int) GraphFileIdx.SUNHOURS)
							{
								GraphDataFiles[i].FtpRequired = false;
							}
						}
						catch (Exception e)
						{
							LogFtpMessage($"{msgPrefix} Error uploading graph data file [{GraphDataFiles[i].FileName}]", false);
							LogFtpMessage($"{msgPrefix} Error = {e}", false);
							FtpAlarm.LastMessage = $"Error uploading file {GraphDataFiles[i].FileName} - {e.Message}";
							FtpAlarm.Triggered = true;
						}
					}
				}

				for (int i = 0; i < GraphDataEodFiles.Length; i++)
				{
					if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
					{
						var localfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].FileName;
						var remotefile = remotePath + GraphDataEodFiles[i].FileName;
						try
						{
							LogFtpMessage($"{msgPrefix} Uploading daily graph data file: {localfile}", false);

							var json = station.CreateEodGraphDataJson(GraphDataEodFiles[i].FileName);

							using var dataStream = GenerateStreamFromString(json);
							if (UploadStream(conn, remotefile, dataStream, cycle1k))
							{
								// Uploaded OK, reset the upload required flag
								GraphDataEodFiles[i].FtpRequired = false;
							}
						}
						catch (Exception e)
						{
							LogFtpMessage($"{msgPrefix} Error uploading daily graph data file [{GraphDataEodFiles[i].FileName}]", false);
							LogFtpMessage($"{msgPrefix} Error = {e}", false);
							FtpAlarm.LastMessage = $"Error uploading file {GraphDataEodFiles[i].FileName} - {e.Message}";
							FtpAlarm.Triggered = true;
						}
					}
				}

				if (MoonImage.Ftp && MoonImage.ReadyToFtp)
				{
					try
					{
						LogFtpMessage("", false);
						LogFtpDebugMessage($"{msgPrefix} Uploading Moon image file", false);
						if (UploadFile(conn, Path.Combine("web", "moon.png"), remotePath + MoonImage.FtpDest, cycle1k))
						{
							// clear the image ready for FTP flag, only upload once an hour
							MoonImage.ReadyToFtp = false;
						}
					}
					catch (Exception e)
					{
						LogErrorMessage($"{msgPrefix} Error uploading moon image - {e.Message}");
						FtpAlarm.LastMessage = $"Error uploading moon image - {e.Message}";
						FtpAlarm.Triggered = true;
					}
				}
			}

			// b3045 - dispose of connection
			conn.Disconnect();
			LogFtpDebugMessage($"{msgPrefix} Disconnected from " + FtpOptions.Hostname, false);
			LogFtpMessage($"{msgPrefix} Process complete", false);
		}

		private async Task UploadPHP(byte cycle, string remotePath)
		{
			var msgPrefix = $"Interval[{cycle}] PHP:";
			var cycle1k = 1000 + cycle;

			LogDebugMessage($"{msgPrefix} Upload process starting");

			var tasklist = new List<Task>();
			var taskCount = 0;
			var runningTaskCount = 0;

			// do we perform a second chance compresssion test?
			if (FtpOptions.PhpCompression == "notchecked")
			{
				TestPhpUploadCompression();
			}

			if (NOAAconf.NeedFtp)
			{
				// upload NOAA Monthly report
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} NOAA Month report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} NOAA Month report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					Interlocked.Increment(ref runningTaskCount);

					if (Program.ExitSystemToken.IsCancellationRequested)
						return false;

					try
					{

						LogDebugMessage($"{msgPrefix} Uploading NOAA Month report");

						var uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestMonthReport);
						var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

						_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, cycle1k, false, NOAAconf.UseUtf8);

					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading NOAA files");
						FtpAlarm.LastMessage = $"Error uploading NOAA files - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} NOAA Year report released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));

				// upload NOAA Annual report
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} NOAA Year report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} NOAA Year report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
							await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						if (Program.ExitSystemToken.IsCancellationRequested)
							return false;

						LogDebugMessage($"{msgPrefix} Uploading NOAA Year report");

						var uploadfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestYearReport);
						var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

						if (await UploadFile(phpUploadHttpClient, uploadfile, remotefile, cycle1k, false, NOAAconf.UseUtf8))
						{
							NOAAconf.NeedFtp = false;
						}

						LogDebugMessage($"{msgPrefix} Upload of NOAA reports complete");
					}
					catch (OperationCanceledException)
					{
						return false;
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading NOAA Year file");
						FtpAlarm.LastMessage = $"Error uploading NOAA files - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} NOAA Year report released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			}

			// Extra files
			LogDebugMessage($"{msgPrefix} Extra Files upload starting");

			for (var i = 0; i < ActiveExtraFiles.Count; i++)
			{
				var item = ActiveExtraFiles[i];

				if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
				{
					continue;
				}

				var data = string.Empty;
				bool incremental = false;
				var linesAdded = 0;
				var idx = i;

				// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
				var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

				var uploadfile = GetUploadFilename(item.local, logDay);
				var remotefile = GetRemoteFileName(item.remote, logDay);

				if (!File.Exists(uploadfile))
				{
					LogWarningMessage($"{msgPrefix} Extra web file - {uploadfile} - not found!");
					return;
				}


				// Is this an incremental log file upload?
				if (item.incrementalLogfile && !item.binary)
				{
					// has the log file rolled over?
					if (item.logFileLastFileName != uploadfile)
					{
						ActiveExtraFiles[i].logFileLastFileName = uploadfile;
						ActiveExtraFiles[i].logFileLastLineNumber = 0;
					}

					incremental = item.logFileLastLineNumber > 0;

					data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

					if (linesAdded == 0)
					{
						LogDebugMessage($"{msgPrefix} Extra file: {uploadfile} - No incremental data found, skipping this upload");
						continue;
					}
				}

				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} Extra file: {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} Extra file: {uploadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						if (Program.ExitSystemToken.IsCancellationRequested)
							return false;

						// all checks OK, file needs to be uploaded
						// Is this an incremental log file upload?
						if (item.incrementalLogfile && !item.binary)
						{
							LogDebugMessage($"{msgPrefix} Uploading extra web incremental file {uploadfile} to {remotefile} ({(incremental ? $"Incremental - {linesAdded} lines" : "Full file")})");
							if (await UploadString(phpUploadHttpClient, incremental, string.Empty, data, remotefile, cycle1k, item.binary, item.UTF8, true, item.logFileLastLineNumber))
							{
								ActiveExtraFiles[idx].logFileLastLineNumber += linesAdded;
							}
						}
						else
						{
							if (item.process)
							{
								LogDebugMessage($"{msgPrefix} Uploading Extra file: {uploadfile} to: {remotefile} (Processed)");

								var str = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
								_ = await UploadString(phpUploadHttpClient, false, string.Empty, str, remotefile, cycle1k, false, item.UTF8);
							}
							else
							{
								LogDebugMessage($"{msgPrefix} Uploading Extra file: {uploadfile} to: {remotefile}");

								_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, cycle1k, false, item.UTF8);
							}
						}
					}
					catch (Exception ex) when (ex is not TaskCanceledException)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading file {uploadfile} to: {remotefile}");
						FtpAlarm.LastMessage = $"Error uploading file {uploadfile} to: {remotefile} - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} Extra file: {uploadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			}

			if (EODfilesNeedFTP)
			{
				EODfilesNeedFTP = false;
			}


			// standard files
			LogDebugMessage($"{msgPrefix} Standard files upload starting");

			StdWebFiles
			.Where(x => x.FTP)
			.ToList()
			.ForEach(item =>
			{
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} Standard Data file: {item.FileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} Standard Data file: {item.FileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						if (Program.ExitSystemToken.IsCancellationRequested)
							return false;

						string data;
						LogDebugMessage($"{msgPrefix} Uploading standard Data file: " + item.FileName);

						if (item.FileName == "wxnow.txt")
						{
							data = station.CreateWxnowFileString();
						}
						else
						{
							data = await ProcessTemplateFile2StringAsync(item.TemplateFileName, true, true);
						}

						if (await UploadString(phpUploadHttpClient, false, string.Empty, data, item.FileName, cycle1k, false, true))
						{
							// No standard files are "one offs" at present
							//StdWebFiles[i].FtpRequired = false
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading file {item.FileName}");
						FtpAlarm.LastMessage = $"Error uploading file {item.FileName} - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} Standard Data file: {item.FileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			});

			// Graph Data Files
			LogDebugMessage($"{msgPrefix} Graph files upload starting");

			var oldest = DateTime.Now.AddHours(-GraphHours);
			var oldestTs = oldest.ToUnixTimeMs().ToString();
			var configFiles = new string[] { "graphconfig.json", "availabledata.json", "dailyrain.json", "dailytemp.json", "sunhours.json" };

			GraphDataFiles
			.Where(x => x.FTP && x.FtpRequired)
			.ToList()
			.ForEach(item =>
			{
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} Graph data file: {item.FileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} Graph data file: {item.FileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						if (Program.ExitSystemToken.IsCancellationRequested)
							return false;

						// we want incremental data for PHP
						var json = station.CreateGraphDataJson(item.FileName, item.Incremental);
						var remotefile = item.FileName;
						LogDebugMessage($"{msgPrefix} Uploading graph data file ({(item.Incremental ? $"incremental from {item.LastDataTime:s}" : "full file")}): {item.FileName}");

						if (string.IsNullOrEmpty(json))
						{
							LogMessage($"{msgPrefix} Uploading to {item.FileName}. No {(item.Incremental ? "incremental" : "")} data found, skipping this upload");
						}
						else
						{
							if (await UploadString(phpUploadHttpClient, item.Incremental, oldestTs, json, remotefile, cycle1k, false, true))
							{
								// The config files only need uploading once per change
								// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
								if (Array.Exists(configFiles, item.FileName.Contains))
								{
									item.FtpRequired = false;
								}
								else
								{
									item.LastDataTime = DateTime.Now;
									item.Incremental = true;
								}
							}
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading graph data file [{item.FileName}]");
						FtpAlarm.LastMessage = $"Error uploading graph data file [{item.FileName}] - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} Graph data file: {item.FileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			});

			// EOD Graph Data Files
			LogDebugMessage($"{msgPrefix} EOD Graph files upload starting");

			GraphDataEodFiles
			.Where(x => x.FTP && x.FtpRequired)
			.ToList()
			.ForEach(item =>
			{
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} Daily graph data file: {item.FileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} Daily graph data file: {item.FileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						if (Program.ExitSystemToken.IsCancellationRequested)
							return false;

						var remotefile = item.FileName;
						LogMessage($"{msgPrefix} Uploading daily graph data file: " + item.FileName);
						var json = station.CreateEodGraphDataJson(item.FileName);

						if (await UploadString(phpUploadHttpClient, false, "", json, remotefile, cycle1k, false, true))
						{
							// Uploaded OK, reset the upload required flag
							item.FtpRequired = false;
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading daily graph data file [{item.FileName}]");
						FtpAlarm.LastMessage = $"Error uploading daily graph data file [{item.FileName}] - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} Daily graph data file: {item.FileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			});

			// Moon image
			LogDebugMessage($"{msgPrefix} Moon image upload starting");

			if (MoonImage.Ftp && MoonImage.ReadyToFtp)
			{
				try
				{
#if DEBUG
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"{msgPrefix} Moon image waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
					LogDebugMessage($"{msgPrefix} Moon image has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref runningTaskCount);

						LogDebugMessage($"{msgPrefix} Uploading Moon image file");

						if (await UploadFile(phpUploadHttpClient, Path.Combine("web", "moon.png"), MoonImage.FtpDest, cycle1k, true))
						{
							// clear the image ready for FTP flag, only upload once an hour
							MoonImage.ReadyToFtp = false;
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"{msgPrefix} Error uploading moon image");
						FtpAlarm.LastMessage = $"Error uploading moon image - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"{msgPrefix} Moon image released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, Program.ExitSystemToken));
			}

			// wait for all the EOD files to start
			LogDebugMessage($"{msgPrefix} Waiting for all tasks to start");
			if (runningTaskCount < taskCount)
			{
				do
				{
					if (Program.ExitSystemToken.IsCancellationRequested)
					{
						LogDebugMessage($"{msgPrefix} Upload process aborted due to program termination");
						return;
					}
					await Task.Delay(10);
				} while (runningTaskCount < taskCount);
			}
			// wait for all the EOD files to complete
			LogDebugMessage($"{msgPrefix} Waiting for all tasks to complete");
			if (tasklist.Count > 0)
			{
				try
				{
					// wait for all the tasks to complete, or timeout
					if (Task.WaitAll([.. tasklist], TimeSpan.FromSeconds(30)))
					{
						LogDebugMessage($"{msgPrefix} Upload process complete, {tasklist.Count} files processed");
					}
					else
					{
						LogErrorMessage($"{msgPrefix} Upload process complete timed out waiting for tasks to complete");
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"{msgPrefix} Error waiting on upload tasks");
					FtpAlarm.LastMessage = "Error waiting on upload tasks";
					FtpAlarm.Triggered = true;
				}
			}
			LogDebugMessage($"{msgPrefix} All tasks completed");

			if (Program.ExitSystemToken.IsCancellationRequested)
			{
				LogDebugMessage($"{msgPrefix} Upload process aborted due to program termination");
				return;
			}

			tasklist.Clear();
			LogDebugMessage($"{msgPrefix} Upload process complete");

			return;
		}
	}
}
