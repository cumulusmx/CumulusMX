
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using DnsClient;

namespace CumulusMX
{
	// A Time-to-live aware DNS resolver with caching
	// Uses the DnsClient library for DNS queries
	// Bypass the OS DNS caching for control over TTL

	public class DnsResolver
	{
		private readonly LookupClient _client;
		private readonly TimeSpan _ttl;
		private IPAddress? _cachedIp;
		private DateTime _lastResolved;

		public DnsResolver(TimeSpan ttl)
		{
			_client = new LookupClient(); // Uses system DNS servers
			_ttl = ttl;
		}

		public async Task<IPAddress> ResolveAsync(string host)
		{
			if (_cachedIp == null || DateTime.UtcNow - _lastResolved > _ttl)
			{
				var result = await _client.QueryAsync(host, QueryType.A); // Try IPv4 first
				var ip = result.Answers.ARecords().FirstOrDefault()?.Address;

				// Optional fallback to AAAA only if IPv6 is enabled
				if (ip == null && Socket.OSSupportsIPv6)
				{
					result = await _client.QueryAsync(host, QueryType.AAAA); // Fallback to IPv6
					ip = result.Answers.AaaaRecords().FirstOrDefault()?.Address;
				}

				_cachedIp = ip ?? throw new InvalidOperationException("No valid IP address found."); 
				_lastResolved = DateTime.UtcNow;
			}

			return _cachedIp!;
		}
	}
}
