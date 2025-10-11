using System;
using System.Net;

using Renci.SshNet;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class SftpClientFactory
	{
		private string _host;
		private int _port;
		private string _authMethod;
		private string _username;
		private string _password;
		private string _pskFile = string.Empty;
		private readonly TimeSpan _dnsTtl;

		public SftpClientFactory(string host, int port, string authMethod, string username, string password, string pskFile, TimeSpan dnsTtl)
		{
			_host = host;
			_port = port;
			_authMethod = authMethod;
			_username = username;
			_password = password;
			_pskFile = pskFile;
			_dnsTtl = dnsTtl;
		}

		private async Task<string> ResolveIp()
		{
			// is the host already supplied as an IP address?
			if (IPAddress.TryParse(_host, out _))
			{
				return _host;
			}

			var resolver = new DnsResolver(_dnsTtl);
			var ip = await resolver.ResolveAsync(_host);
			return ip.ToString();
		}

		public async Task<SftpClient> CreateClient()
		{
			var ip = await ResolveIp();

			ConnectionInfo connectionInfo;

			if (_authMethod == "password")
			{
				connectionInfo = new ConnectionInfo(ip, _port, _username, new PasswordAuthenticationMethod(_username, _password));
			}
			else if (_authMethod == "psk")
			{
				PrivateKeyFile pskFile = new PrivateKeyFile(_pskFile);
				connectionInfo = new ConnectionInfo(ip.ToString(), _port, _username, new PrivateKeyAuthenticationMethod(_username, pskFile));
			}
			else if (_authMethod == "password_psk")
			{
				PrivateKeyFile pskFile = new PrivateKeyFile(_pskFile);
				connectionInfo = new ConnectionInfo(ip.ToString(), _port, _username, new PasswordAuthenticationMethod(_username, _password), new PrivateKeyAuthenticationMethod(_username, pskFile));
			}
			else
			{
				throw new ArgumentException("Invalid authentication method: " + _authMethod);
			}

			connectionInfo.Timeout = TimeSpan.FromSeconds(30);

			var client = new SftpClient(connectionInfo)
			{
				OperationTimeout = TimeSpan.FromSeconds(60)
			};

			return client;
		}

		public string Host { set => _host = value; }
		public int Port { set => _port = value; }
		public string AuthMethod { set => _authMethod = value; }
		public string Username { set => _username = value; }
		public string Password { set => _password = value; }
		public string PskFile { set => _pskFile = value; }
	}
}
