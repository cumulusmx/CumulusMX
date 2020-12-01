using System;
using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using FluentFTP;

namespace CumulusMX
{
	public class MxFtpClient
	{
		public bool Connected;
		public bool Reconnecting;

		private readonly Cumulus cumulus;
		private readonly FtpClient ftpClient;
		private readonly bool persistent;

		private static readonly CustomTraceListener FtpTraceListener = new CustomTraceListener("ftplog.txt", "ftplog");

		/// <summary>
		/// Creates the FTP client
		/// </summary>
		/// <param name="cumulus">The parent object, required for all sorts of config "stuff"</param>
		public MxFtpClient(Cumulus cumulus, bool persist)
		{
			this.cumulus = cumulus;
			persistent = persist;

			ftpClient = new FtpClient()
			{
				Host = cumulus.FtpHostname,
				Port = cumulus.FtpHostPort,
				Credentials = new NetworkCredential(cumulus.FtpUsername, cumulus.FtpPassword),
				DataConnectionType = cumulus.ActiveFTPMode
					? FtpDataConnectionType.AutoActive
					: FtpDataConnectionType.AutoPassive,
				SocketKeepAlive = true,
			};

			if (cumulus.DisableFtpsEPSV)
			{
				ftpClient.DataConnectionType = FtpDataConnectionType.PASV;
			}

			// Additional settings for SFTP
			if (cumulus.Sslftp == Cumulus.FtpProtocols.FTPS)
			{
				ftpClient.DataConnectionEncryption = true;
				ftpClient.ValidateAnyCertificate = true;
				ftpClient.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
				ftpClient.EncryptionMode = cumulus.DisableFtpsExplicit
					? FtpEncryptionMode.Implicit
					: FtpEncryptionMode.Explicit;
			}
		}

		~MxFtpClient()
		{
			if (ftpClient == null || !ftpClient.IsConnected)
				return;

			try
			{
				ftpClient.Disconnect();
			}
			catch
			{
				// do nothing
			}
		}

		/// <summary>
		/// Connects to the FTP server
		/// </summary>
		/// <param name="cycle">Optional realtime upload cycle number for logging</param>
		public async void Connect(int cycle = -1)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Connecting...");
			try
			{
				await ftpClient.ConnectAsync();
				Connected = true;
				cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Connected OK");
			}
			catch (FtpAuthenticationException e)
			{
				cumulus.LogFtpMessage($"FTP[{cycleStr}]: Authenication error - {e}");
				Connected = false;
			}
			catch (FtpSecurityNotAvailableException e)
			{
				if (e.Message.Contains("'TLS' not supported"))
				{
					cumulus.LogFtpMessage($"FTP[{cycleStr}]: The FTP server does not support FTPS");
				}
				else
				{
					cumulus.LogFtpMessage($"FTP[{cycleStr}]: FTP security error: {e}");
				}

				Connected = false;
			}
			catch (Exception e)
			{
				cumulus.LogFtpMessage($"FTP[{cycleStr}]: Error - {e}");
				if (e.InnerException != null)
				{
					cumulus.LogFtpMessage($"FTP[{cycleStr}]: Inner error - {e}");
				}

				Connected = false;
			}

			// Are we a persistent connection?
			// And the connect failed, and we are not already attempting to reconnect, then we need to fall back into reconnect mode
			if (persistent && !Connected && !Reconnecting)
			{
				Reconnecting = true;
				// TODO: Add the reconnecting code!
			}
		}

		/// <summary>
		/// Disconnects from the FTP server
		/// </summary>
		/// <param name="cycle">Optional realtime upload cycle number for logging</param>
		public void Disconnect(int cycle = -1)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Disconnecting...");
			ftpClient.Disconnect();
			cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Disconnected OK");
		}

		/// <summary>
		/// Attempts to reconnect to the FTP server
		/// </summary>
		/// <returns>Connection status, true = connected, false = failed</returns>
		public bool Reconnect()
		{
			cumulus.LogFtpDebugMessage("FTP: Reconnecting...");

			// In case the user has changed the config, reset the connection parameters
			ftpClient.Port = cumulus.FtpHostPort;
			ftpClient.Credentials = new NetworkCredential(cumulus.FtpUsername, cumulus.FtpPassword);
			ftpClient.DataConnectionType = cumulus.ActiveFTPMode
				? FtpDataConnectionType.AutoActive
				: FtpDataConnectionType.AutoPassive;
			ftpClient.EncryptionMode = cumulus.DisableFtpsExplicit
				? FtpEncryptionMode.Implicit
				: FtpEncryptionMode.Explicit;

			if (cumulus.DisableFtpsEPSV)
			{
				ftpClient.DataConnectionType = FtpDataConnectionType.PASV;
			}

			if (cumulus.Sslftp == Cumulus.FtpProtocols.FTPS)
			{
				cumulus.LogFtpDebugMessage("FTP: Using FTPS protocol");
				ftpClient.DataConnectionEncryption = true;
				ftpClient.ValidateAnyCertificate = true;
				ftpClient.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
				ftpClient.DataConnectionEncryption = true;
				ftpClient.EncryptionMode = cumulus.DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
			}

			try
			{
				ftpClient.Connect();
				cumulus.LogFtpMessage("FTP: Reconnected OK");
				Connected = true;
			}
			catch (FtpAuthenticationException e)
			{
				cumulus.LogFtpMessage($"FTP: Authenication error - {e}");
				Connected = false;
			}
			catch (Exception e)
			{
				cumulus.LogFtpMessage($"FTP: Error - {e}");
				if (e.InnerException != null)
				{
					cumulus.LogFtpMessage($"FTP: Inner error - {e}");
				}
				Connected = false;
			}
			return Connected;
		}

		/// <summary>
		/// Uploads a file
		/// </summary>
		/// <param name="localFile">Source filename</param>
		/// <param name="remoteFile">Destination filename</param>
		/// <param name="cycle">Optional realtime upload cycle number for logging</param>
		/// <returns>True on success, False on failure</returns>
		public bool UploadFile(string localFile, string remoteFile, int cycle = -1)
		{
			bool success = true;
			string uploadFilename = cumulus.FTPRename ? remoteFile + "tmp" : remoteFile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {localFile} to {remoteFile}");

			// Do we have an FTP connection?
			if (!Connected || !IsConnected)
			{
				cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Error, FTP client is not connected, aborting upload");
				return false;
			}

			try
			{
				if (cumulus.DeleteBeforeUpload)
				{
					// delete the existing file
					try
					{
						ftpClient.DeleteFile(remoteFile);
					}
					catch (Exception ex)
					{
						cumulus.LogFtpMessage($"FTP[{cycleStr}]: Error deleting {remoteFile} : {ex.Message}");
					}
				}

				try
				{
					ftpClient.UploadFile(localFile, uploadFilename);
				}
				catch (Exception ex)
				{
					cumulus.LogFtpMessage($"FTP[{cycleStr}]: Error uploading {localFile} to {uploadFilename} : {ex.Message}");
					success = false;
				}

				if (cumulus.FTPRename)
				{
					// rename the file
					try
					{
						ftpClient.Rename(uploadFilename, remoteFile);
					}
					catch (Exception ex)
					{
						cumulus.LogFtpMessage($"FTP[{cycleStr}]: Error renaming {uploadFilename} to {remoteFile} : {ex.Message}");
						success = false;
					}
				}

				cumulus.LogFtpDebugMessage($"FTP[{cycleStr}]: Completed uploading {localFile} to {remoteFile}");
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"FTP[{cycleStr}]: Error uploading {localFile} to {remoteFile} : {ex.Message}");
				success = false;
			}

			return success;
		}

		/// <summary>
		/// Tests if the server connection is still alive
		/// </summary>
		/// <returns>True = alive, False = dead</returns>
		public bool TestConnection()
		{
			try
			{
				if (ftpClient.GetWorkingDirectory() != string.Empty)
				{
					Connected = true;
					return true;
				}
			}
			catch (Exception e)
			{
				//cumulus.LogFtpDebugMessage($"FTP: Error in GetWorkingDirectory - {e}");
			}

			Connected = false;
			return false;
		}

		public bool IsConnected => ftpClient?.IsConnected ?? false;

		/// <summary>
		/// Enables or disables FTP logging
		/// </summary>
		/// <param name="isSet">Enable or disable logging</param>
		public void SetFtpLogging(bool isSet)
		{
			try
			{
				FtpTrace.RemoveListener(FtpTraceListener);
			}
			catch
			{
				// ignored
			}

			if (isSet)
			{
				FtpTrace.AddListener(FtpTraceListener);
				FtpTrace.FlushOnWrite = true;
			}
		}


		/// <summary>
		/// Custom trace listener class that adds a date/time stamp to all entries
		/// </summary>
		private class CustomTraceListener : TextWriterTraceListener
		{
			public CustomTraceListener(string filename, string name) : base(filename, name)
			{
				// nothing to do here
			}

			public override void WriteLine(string message)
			{
				base.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff "));
				base.WriteLine(message);
			}
		}
	}
}
