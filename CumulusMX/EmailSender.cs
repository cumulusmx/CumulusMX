using System;
using MailKit.Net.Smtp;
using MailKit.Net.Pop3;
using MimeKit;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MailKit;

namespace CumulusMX
{
	public class EmailSender
	{
		static readonly Regex ValidEmailRegex = CreateValidEmailRegex();

		private readonly Cumulus cumulus;

		public EmailSender(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}


		public async void SendEmail(string[] to, string from, string subject, string message, bool isHTML)
		{
			try
			{
				cumulus.LogDebugMessage($"SendEmail: Sending email, to [{string.Join("; ", to)}], subject [{subject}], body [{message}]...");

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

				using (SmtpClient client = new SmtpClient(cumulus.SmtpOptions.Logging ? new ProtocolLogger("MXdiags/smtp.log") : null))
				{
					await client.ConnectAsync(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
					//client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);

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
			}
			catch (Exception e)
			{
				cumulus.LogMessage("SendEmail: Error - " + e);

			}
		}

		public void SendTestEmail(string[] to, string from, string subject, string message, bool isHTML)
		{
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

			using (SmtpClient client = new SmtpClient(cumulus.SmtpOptions.Logging ? new ProtocolLogger("MXdiags/smtp.log") : null))
			{
				client.Connect(cumulus.SmtpOptions.Server, cumulus.SmtpOptions.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
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
		}


		private static Regex CreateValidEmailRegex()
		{
			string validEmailPattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|"
				+ @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)"
				+ @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";

			return new Regex(validEmailPattern, RegexOptions.IgnoreCase);
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
			public bool UseSsl;
			public bool RequiresAuthentication;
			public bool Logging;
		}
	}
}
