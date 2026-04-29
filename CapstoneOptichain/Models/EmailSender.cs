using System.Net.Mail;
using System.Net;

namespace CapstoneOptichain.Models
{
	public class EmailSender
	{
		private readonly IConfiguration _config;

		public EmailSender(IConfiguration config)
		{
			_config = config;
		}

		public async Task<bool> SendCodeAsync(string toEmail, string code)
		{
			try
			{
				var smtpHost = _config["EmailSettings:Host"]
					?? throw new ArgumentNullException("SMTP Host is missing in config");

				if (!int.TryParse(_config["EmailSettings:Port"], out var smtpPort))
				{
					throw new ArgumentException("Invalid SMTP Port in config");
				}

				var smtpUser = _config["EmailSettings:UserName"]
					?? throw new ArgumentNullException("SMTP Username is missing");

				var smtpPass = _config["EmailSettings:Password"]?.Trim()
					?? throw new ArgumentNullException("SMTP Password is missing");

				if (!bool.TryParse(_config["EmailSettings:EnableSSL"], out var enableSSL))
				{
					enableSSL = true; // Default value
				}

				using var mail = new MailMessage
				{
					From = new MailAddress(smtpUser, "OptiChain"),
					Subject = "Verification Code - OptiChain",
					Body = $"<h3>Your verification code is: {code}</h3><p>Valid for 10 minutes</p>",
					IsBodyHtml = true
				};
				mail.To.Add(toEmail);

				using var smtp = new SmtpClient(smtpHost, smtpPort)
				{
					Credentials = new NetworkCredential(smtpUser, smtpPass),
					EnableSsl = enableSSL,
					DeliveryMethod = SmtpDeliveryMethod.Network,
					Timeout = 10000
				};

				await smtp.SendMailAsync(mail);
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"SMTP Error: {ex.Message}");
				Console.WriteLine($"Stack Trace: {ex.StackTrace}");
				return false;
			}
		}
	}
}
