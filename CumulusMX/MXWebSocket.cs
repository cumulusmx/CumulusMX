using System.Threading.Tasks;

using EmbedIO.WebSockets;

namespace CumulusMX
{
	public class MxWebSocket(string urlPath, Cumulus cumulus) : WebSocketModule(urlPath, true)
	{
		private readonly Cumulus cumulus = cumulus;
		private WeatherStation station;

		/// <inheritdoc />
		protected override async Task OnClientConnectedAsync(IWebSocketContext context)
		{
			cumulus.LogDebugMessage("WS Client Connect: " + context.RemoteEndPoint.Address.ToString() + ", Total clients: " + ConnectedClients);
			if (station != null)
			{
				// send an update right away so the client is not left waiting
				await SendAsync(context, station.GetCurrentData());
			}
			await Task.CompletedTask;
		}

		/// <inheritdoc />
		protected override async Task OnClientDisconnectedAsync(IWebSocketContext context)
		{
			cumulus.LogDebugMessage("WS Client Disconnected: " + context.RemoteEndPoint.Address.ToString() + ", Total clients: " + ConnectedClients);
			await Task.CompletedTask;
		}

		protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
		{
			await Task.CompletedTask;
		}

		// We will do this synchronously to avoid overlaps
		public void SendMessage(string message)
		{
			BroadcastAsync(message).Wait();
		}

		public int ConnectedClients
		{
			get { return ActiveContexts.Count; }
		}

		internal WeatherStation SetStation
		{
			set { station = value; }
		}
	}
}
