using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

namespace CumulusMX
{
	public class EmailSender
	{
		static readonly Regex ValidEmailRegex = CreateValidEmailRegex();
		private static SemaphoreSlim _writeLock;
		private readonly Cumulus cumulus;
		private static readonly string[] initializer = new[] { "https://www.googleapis.com/auth/gmail.send" };

		public EmailSender(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			_writeLock = new SemaphoreSlim(1);
		}


		public async Task<bool> SendEmail(string[] to, string from, string subject, string message, bool isHTML, bool useBcc)
		{
			bool retVal = false;

			if (string.IsNullOrEmpty(cumulus.SmtpOptions.Server) || string.IsNullOrEmpty(cumulus.SmtpOptions.User))
			{
				cumulus.LogWarningMessage("SendEmail: You have not configured either the email server, or the email account used to send email");
				return retVal;
			}

			try
			{
				//cumulus.LogDebugMessage($"SendEmail: Waiting for lock...");
				await _writeLock.WaitAsync();
				//cumulus.LogDebugMessage($"SendEmail: Has the lock");

				var logMessage = ToLiteral(message);
				var sendSubject = subject + " - " + cumulus.LocationName;

				cumulus.LogMessage($"SendEmail: Sending email, to [{string.Join("; ", to)}], subject [{sendSubject}], body [{logMessage}]...");

				var m = new MimeMessage();
				m.From.Add(new MailboxAddress("", from));
				foreach (var addr in to)
				{
					if (useBcc)
						m.Bcc.Add(new MailboxAddress("", addr));
					else
						m.To.Add(new MailboxAddress("", addr));
				}

				m.Subject = sendSubject;

				BodyBuilder bodyBuilder = new BodyBuilder();
				if (isHTML)
				{
					bodyBuilder.HtmlBody = message;
				}
				else
				{
					bodyBuilder.TextBody = message;
				}

				m.Body = bodyBuilder.ToMessageBody();

				using (SmtpClient client = cumulus.SmtpOptions.Logging ? new SmtpClient(new ProtocolLogger("MXdiags/smtp.log")) : new SmtpClient())
				{
					if (cumulus.SmtpOptions.IgnoreCertErrors)
					{
						client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					}

					await client.ConnectAsync(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (SecureSocketOptions) cumulus.SmtpOptions.SslOption);

					// 0 = None
					// 1 = Username/Passwword
					// 2 = Google OAuth2
					if (cumulus.SmtpOptions.AuthenticationMethod <= 1)
					{
						// Note: since we don't have an OAuth2 token, disable
						// the XOAUTH2 authentication mechanism.
						client.AuthenticationMechanisms.Remove("XOAUTH2");

						if (cumulus.SmtpOptions.AuthenticationMethod == 1)
						{
							await client.AuthenticateAsync(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
							//client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						}
					}
					else if (cumulus.SmtpOptions.AuthenticationMethod == 2)
					{
						// Google OAuth 2.0
						var clientSecrets = new ClientSecrets
						{
							ClientId = cumulus.SmtpOptions.ClientId,
							ClientSecret = cumulus.SmtpOptions.ClientSecret
						};

						var codeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
						{
							DataStore = new FileDataStore(cumulus.AppDir, false),
							Scopes = initializer,
							ClientSecrets = clientSecrets
						});

						var codeReceiver = new LocalServerCodeReceiver();
						var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);

						var credential = await authCode.AuthorizeAsync(cumulus.SmtpOptions.User, CancellationToken.None);

						if (credential.Token.IsStale)
						{
							await credential.RefreshTokenAsync(CancellationToken.None);
						}

						var oauth2 = new SaslMechanismOAuth2(credential.UserId, credential.Token.AccessToken);

						await client.AuthenticateAsync(oauth2);
					}

					var response = await client.SendAsync(m);
					cumulus.LogDebugMessage("SendEmail response: " + response);
					await client.DisconnectAsync(true);
				}
				retVal = true;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "SendEmail: Error");
			}
			finally
			{
				//cumulus.LogDebugMessage($"SendEmail: Releasing lock...");
				_writeLock.Release();
			}
			return retVal;
		}

		public string SendTestEmail(string[] to, string from, string subject, string message, bool isHTML)
		{
			string retVal;

			if (string.IsNullOrEmpty(cumulus.SmtpOptions.Server) || string.IsNullOrEmpty(cumulus.SmtpOptions.User))
			{
				cumulus.LogWarningMessage("SendEmail: You have not configured either the email server, or the email account used to send email");
				return "You have not configured either the email server, or the email account used to send email";
			}

			try
			{
				//cumulus.LogDebugMessage($"SendEmail: Waiting for lock...");
				_writeLock.Wait();
				//cumulus.LogDebugMessage($"SendEmail: Has the lock");

				cumulus.LogDebugMessage($"SendEmail: Sending Test email, to [{string.Join("; ", to)}], subject [{subject}], body [{message}]...");

				var m = new MimeMessage();
				m.From.Add(new MailboxAddress("", from));
				foreach (var addr in to)
				{
					m.To.Add(new MailboxAddress("", addr));
				}
				m.Subject = subject;

				BodyBuilder bodyBuilder = new BodyBuilder();
				if (isHTML)
				{
					bodyBuilder.HtmlBody = message;
				}
				else
				{
					bodyBuilder.TextBody = message;
				}

				m.Body = bodyBuilder.ToMessageBody();

				using (SmtpClient client = cumulus.SmtpOptions.Logging ? new SmtpClient(new ProtocolLogger("MXdiags/smtp.log")) : new SmtpClient())
				{
					client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (MailKit.Security.SecureSocketOptions) cumulus.SmtpOptions.SslOption);
					//client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);

					if (cumulus.SmtpOptions.AuthenticationMethod <= 1)
					{
						// Note: since we don't have an OAuth2 token, disable
						// the XOAUTH2 authentication mechanism.
						client.AuthenticationMechanisms.Remove("XOAUTH2");

						if (cumulus.SmtpOptions.AuthenticationMethod == 1)
						{
							client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
							//client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						}
					}
					else if (cumulus.SmtpOptions.AuthenticationMethod == 2)
					{
						// Google OAuth 2.0
						var clientSecrets = new ClientSecrets
						{
							ClientId = cumulus.SmtpOptions.ClientId,
							ClientSecret = cumulus.SmtpOptions.ClientSecret
						};

						var codeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
						{
							DataStore = new FileDataStore(cumulus.AppDir, false),
							Scopes = initializer,
							ClientSecrets = clientSecrets
						});

						var codeReceiver = new LocalServerCodeReceiver();
						var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);

						var credential = authCode.AuthorizeAsync(cumulus.SmtpOptions.User, CancellationToken.None).Result;

						if (credential.Token.IsStale)
						{
							_ = credential.RefreshTokenAsync(CancellationToken.None).Result;
						}

						var oauth2 = new SaslMechanismOAuth2(credential.UserId, credential.Token.AccessToken);

						client.Authenticate(oauth2);
					}

					var response = client.Send(m);
					cumulus.LogDebugMessage("SendEmail response: " + response);
					client.Disconnect(true);
				}

				retVal = "OK";
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("SendEmail: Error - " + e);
				retVal = e.Message;
			}
			finally
			{
				//cumulus.LogDebugMessage($"SendEmail: Releasing lock...");
				_writeLock.Release();
			}

			return retVal;
		}


		private static Regex CreateValidEmailRegex()
		{
			string validEmailPattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|"
				+ @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)"
				+ @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";

			return new Regex(validEmailPattern, RegexOptions.IgnoreCase);
		}

		private static string ToLiteral(string input)
		{
			using var writer = new StringWriter();
			using var provider = CodeDomProvider.CreateProvider("CSharp");
			provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
			return writer.ToString();
		}

		public static bool CheckEmailAddress(string email)
		{
			if (email == null)
				return true;

			return ValidEmailRegex.IsMatch(email);
		}

		public class SmtpOptions
		{
			public bool Enabled;
			public string Server;
			public int Port;
			public string User;
			public string Password;
			public string ClientId;
			public string ClientSecret;
			public int SslOption;
			public int AuthenticationMethod;
			public bool Logging;
			public bool IgnoreCertErrors;
		}
	}
}
