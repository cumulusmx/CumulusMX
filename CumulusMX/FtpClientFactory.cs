using System;
using System.Net;
using System.Threading.Tasks;

using FluentFTP;

using static CumulusMX.Cumulus;

namespace CumulusMX
{
	public class FtpClientFactory
	{
		private string _host;
		private int _port;
		private string _username;
		private string _password;
		private FtpProtocols _protocol;
		private bool _autodetect;
		private bool _disableExplicit;
		private bool _activeMode;
		private bool _disableEpsv;
		private bool _ignoreCerts;
		private readonly TimeSpan _dnsTtl;
		private readonly DnsResolver _dnsResolver;

		public FtpClientFactory(string host, int port, string username, string password, FtpProtocols protocol, bool autodetect, bool disableExplicit, bool activeMode, bool disableEpsv, bool ignoreCerts, TimeSpan dnsTtl)
		{
			_host = host;
			_port = port;
			_username = username;
			_password = password;
			_protocol = protocol;
			_autodetect = autodetect;
			_disableExplicit = disableExplicit;
			_activeMode = activeMode;
			_disableEpsv = disableEpsv;
			_ignoreCerts = ignoreCerts;
			_dnsTtl = dnsTtl;
			_dnsResolver = new DnsResolver(_dnsTtl);
		}

		private async Task<string> ResolveIp()
		{
			// is the host already supplied as an IP address?
			if (IPAddress.TryParse(_host, out _))
			{
				return _host;
			}

			var ip = await _dnsResolver.ResolveAsync(_host);
			return ip.ToString();
		}

		public async Task<FtpClient> CreateClient()
		{
			var ip = await ResolveIp();

			var client = new FtpClient
			{
				//Enabled = false,
				Host = ip,
				Port = _port,
				Credentials = new NetworkCredential(_username, _password),
			};

			client.Config.LogPassword = false;
			if (!_autodetect)
			{
				if (_protocol == FtpProtocols.FTPS)
				{
					client.Config.EncryptionMode = _disableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
					client.Config.DataConnectionEncryption = true;
					client.Config.ValidateAnyCertificate = _ignoreCerts;
				}

				if (_activeMode)
				{
					client.Config.DataConnectionType = FtpDataConnectionType.PORT;
				}
				else if (_disableEpsv)
				{
					client.Config.DataConnectionType = FtpDataConnectionType.PASV;
				}
				else
				{
					client.Config.DataConnectionType = FtpDataConnectionType.EPSV;
				}
			}

			return client;
		}

		public string Host { set => _host = value; }
		public int Port { set => _port = value; }
		public string Username { set => _username = value; }
		public string Password { set => _password = value; }
		public FtpProtocols Protocol { set => _protocol = value; }
		public bool Autodetect { set => _autodetect = value; }
		public bool DisableExplicit { set => _disableExplicit = value; }
		public bool ActiveMode { set => _activeMode = value; }
		public bool DisableEpsv { set => _disableEpsv = value; }
		public bool IgnoreCertErrors { set => _ignoreCerts = value; }
	}
}
