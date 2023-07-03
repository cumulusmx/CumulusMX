using System;
using MailKit.Net.Smtp;
using MimeKit;
using System.Text.RegularExpressions;
using MailKit;
using System.Threading;
using System.IO;
using System.CodeDom.Compiler;
using System.CodeDom;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class EmailSender
	{
		static readonly Regex ValidEmailRegex = CreateValidEmailRegex();
		private static SemaphoreSlim _writeLock;
		private readonly Cumulus cumulus;

		public EmailSender(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			_writeLock = new SemaphoreSlim(1);
		}


		public async Task<bool> SendEmail(string[] to, string from, string subject, string message, bool isHTML)
		{
			bool retVal = false;
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
					m.Bcc.Add(new MailboxAddress("", addr));
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

					await client.ConnectAsync(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (MailKit.Security.SecureSocketOptions)cumulus.SmtpOptions.SslOption);

					// Note: since we don't have an OAuth2 token, disable
					// the XOAUTH2 authentication mechanism.
					client.AuthenticationMechanisms.Remove("XOAUTH2");

					if (cumulus.SmtpOptions.RequiresAuthentication)
					{
						await client.AuthenticateAsync(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						//client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
					}

					await client.SendAsync(m);
					client.Disconnect(true);
				}
				retVal = true;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("SendEmail: Error - " + e);
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
					client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (MailKit.Security.SecureSocketOptions)cumulus.SmtpOptions.SslOption);
					//client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);

					// Note: since we don't have an OAuth2 token, disable
					// the XOAUTH2 authentication mechanism.
					client.AuthenticationMechanisms.Remove("XOAUTH2");

					if (cumulus.SmtpOptions.RequiresAuthentication)
					{
						client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						//client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
					}

					client.Send(m);
					client.Disconnect(true);
				}

				retVal = "OK";
			}
			catch (Exception e)
			{
				cumulus.LogMessage("SendEmail: Error - " + e);
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
			using (var writer = new StringWriter())
			{
				using (var provider = CodeDomProvider.CreateProvider("CSharp"))
				{
					provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
					return writer.ToString();
				}
			}
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
			public int SslOption;
			public bool RequiresAuthentication;
			public bool Logging;
			public bool IgnoreCertErrors;
		}
	}
}
