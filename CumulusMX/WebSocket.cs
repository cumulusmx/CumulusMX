﻿using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using System.Net;

namespace CumulusMX
{
	public static class WebSocket
	{
		private static Cumulus cumulus;
		private static MySocketServer socketServer;

		/// <summary>
		/// Sets up the specified server.
		/// </summary>
		/// <param name="server">The server.</param>
		public static void Setup(WebServer server, Cumulus cumulus)
		{
			WebSocket.cumulus = cumulus;
			server.RegisterModule(new WebSocketsModule());
			server.Module<WebSocketsModule>().RegisterWebSocketsServer<MySocketServer>();
		}

		/// <summary>
		/// Sends a message to the WebSocketServer for broadcast to all connected clients
		/// </summary>
		/// <param name="message">The message to send.</param>
		public static void SendMessage(string message)
		{
			socketServer.SendMessage(message);
		}

		/// <inheritdoc />
		/// <summary>
		/// Defines a very simple web socket server
		/// </summary>
		[WebSocketHandler("/ws")]
		private class MySocketServer : WebSocketsServer
		{
			public MySocketServer() : base(true, 0)
			{
				socketServer = this;
			}

			/// <inheritdoc />
			public override string ServerName
			{
				get
				{
					return nameof(MySocketServer);
				}
			}

			/// <inheritdoc />
			protected override void OnClientConnected(IWebSocketContext context, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
			{
				cumulus.LogDebugMessage("WS Connect From : " + remoteEndPoint.Address.ToString());
			}

			/// <inheritdoc />
			protected override void OnClientDisconnected(IWebSocketContext context)
			{
				cumulus.LogDebugMessage("WS Client Disconnected");
			}

			/// <inheritdoc />
			protected override void OnFrameReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
			{
				//cumulus.LogDebugMessage("WS receive : " + buffer.ToString());
			}

			/// <inheritdoc />
			protected override void OnMessageReceived(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
			{
				//cumulus.LogDebugMessage("WS receive : " + buffer.ToString());
			}

			public void SendMessage(string message)
			{
				Broadcast(message);
			}
		}
	}
}
