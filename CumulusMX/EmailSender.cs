using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

namespace CumulusMX
{
	public class EmailSender(Cumulus cumulus)
	{
		static readonly Regex ValidEmailRegex = CreateValidEmailRegex();
		private static readonly SemaphoreSlim _writeLock = new(1);
		private readonly Cumulus cumulus = cumulus;

		public async Task<bool> SendEmail(string[] to, string from, string subject, string message, bool isHTML, bool useBcc)
		{
			bool retVal = false;

			if (string.IsNullOrEmpty(cumulus.SmtpOptions.Server) || (cumulus.SmtpOptions.AuthenticationMethod > 0 && string.IsNullOrEmpty(cumulus.SmtpOptions.User)))
			{
				cumulus.LogWarningMessage("SendEmail: You have not configured either the email server or the email account used to send email");
				return retVal;
			}

			try
			{
				await _writeLock.WaitAsync();

				var logMessage = ToLiteral(message);
				var sendSubject = subject + " - " + cumulus.LocationName;

				var logMsg = $"SendEmail: Sending email, to [{string.Join("; ", to)}], subject [{sendSubject}], body [{logMessage}]";
				cumulus.LogMessage(logMsg);

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

					if (cumulus.SmtpOptions.Logging)
					{
						logMsg = DateTime.Now.ToString("\n\nyyyy-MM-dd HH:mm:ss.fff ") + logMsg;
						var byteArr = System.Text.Encoding.UTF8.GetBytes(logMsg);
						client.ProtocolLogger.LogClient(byteArr, 0, byteArr.Length);
					}

					await client.ConnectAsync(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (SecureSocketOptions) cumulus.SmtpOptions.SslOption);

					// 0 = None
					// 1 = Username/Passwword
					if (cumulus.SmtpOptions.AuthenticationMethod <= 1)
					{
						// Note: since we don't have an OAuth2 token, disable
						// the XOAUTH2 authentication mechanism.
						client.AuthenticationMechanisms.Remove("XOAUTH2");

						if (cumulus.SmtpOptions.AuthenticationMethod == 1)
						{
							await client.AuthenticateAsync(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						}
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
				_writeLock.Release();
			}
			return retVal;
		}

		public string SendTestEmail(string[] to, string from, string subject, string message, bool isHTML)
		{
			string retVal;

			if (string.IsNullOrEmpty(cumulus.SmtpOptions.Server) || (cumulus.SmtpOptions.AuthenticationMethod > 0 && string.IsNullOrEmpty(cumulus.SmtpOptions.User)))
			{
				cumulus.LogWarningMessage("SendEmail: You have not configured either the email server or the email account used to send email");
				return "You have not configured either the email server or the email account used to send email";
			}

			try
			{
				_writeLock.Wait();

				var logMsg = $"SendEmail: Sending TEST email, to [{string.Join("; ", to)}], subject [{subject}], body [{subject}]";
				cumulus.LogMessage(logMsg);

				var m = new MimeMessage();
				m.From.Add(new MailboxAddress("", from));
				foreach (var addr in to)
				{
					if (cumulus.AlarmEmailUseBcc)
						m.Bcc.Add(new MailboxAddress("", addr));
					else
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
					if (cumulus.SmtpOptions.IgnoreCertErrors)
					{
						client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					}

					if (cumulus.SmtpOptions.Logging)
					{
						logMsg = DateTime.Now.ToString("\n\nyyyy-MM-dd HH:mm:ss.fff ") + logMsg;
						var byteArr = System.Text.Encoding.UTF8.GetBytes(logMsg);
						client.ProtocolLogger.LogClient(byteArr, 0, byteArr.Length);
					}

					client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, (MailKit.Security.SecureSocketOptions) cumulus.SmtpOptions.SslOption);

					// 0 = None
					// 1 = Username/Passwword
					if (cumulus.SmtpOptions.AuthenticationMethod <= 1)
					{
						// Note: since we don't have an OAuth2 token, disable
						// the XOAUTH2 authentication mechanism.
						client.AuthenticationMechanisms.Remove("XOAUTH2");

						if (cumulus.SmtpOptions.AuthenticationMethod == 1)
						{
							client.Authenticate(cumulus.SmtpOptions.User, cumulus.SmtpOptions.Password);
						}
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
			public bool Enabled { get; set; }
			public string Server { get; set; }
			public int Port { get; set; }
			public string User { get; set; }
			public string Password { get; set; }
			public int SslOption { get; set; }
			public int AuthenticationMethod { get; set; }
			public bool Logging { get; set; }
			public bool IgnoreCertErrors { get; set; }
		}
	}
}
