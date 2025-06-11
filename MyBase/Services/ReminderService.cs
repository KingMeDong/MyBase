using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBase.Data;
using MyBase.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace MyBase.Services {
    public class ReminderService : BackgroundService {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReminderService> _logger;
        private readonly SmtpSettings _smtpSettings;

        
        public ReminderService(IServiceProvider serviceProvider, ILogger<ReminderService> logger, Microsoft.Extensions.Options.IOptions<SmtpSettings> smtpOptions) {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _smtpSettings = smtpOptions.Value;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                await CheckRemindersAsync();

                // 1 Minute warten
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckRemindersAsync() {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var upcomingEvents = await dbContext.CalendarEvents
                .Where(e => e.IsReminderEnabled && !e.ReminderSent && e.ReminderEmailAddress != null)
                .ToListAsync();

            foreach (var evt in upcomingEvents) {
                if (evt.ReminderMinutesBefore == null) continue;

                var reminderTime = evt.StartDateTime.AddMinutes(-evt.ReminderMinutesBefore.Value);

                if (now >= reminderTime && now < evt.StartDateTime) {
                    // E-Mail senden
                    SendReminderEmail(evt);

                    // Reminder als gesendet markieren
                    evt.ReminderSent = true;
                    _logger.LogInformation($"Reminder gesendet für Termin: {evt.Title} an {evt.ReminderEmailAddress}");
                }
            }

            await dbContext.SaveChangesAsync();
        }

        private void SendReminderEmail(CalendarEvent evt) {
            try {
                var smtpClient = new SmtpClient(_smtpSettings.Host) {
                    Port = _smtpSettings.Port,
                    Credentials = new System.Net.NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage {
                    From = new MailAddress(_smtpSettings.FromEmail),
                    Subject = $"🕑 Erinnerung: {evt.Title}",
                    Body = $"Dies ist eine Erinnerung für den Termin \"{evt.Title}\".\n\nStart: {evt.StartDateTime:G}\nOrt: {evt.Location}\n\nBeschreibung:\n{evt.Description}",
                    IsBodyHtml = false,
                };

                mailMessage.To.Add(evt.ReminderEmailAddress!);

                smtpClient.Send(mailMessage);
            } catch (Exception ex) {
                _logger.LogError(ex, $"Fehler beim Senden des Reminders für {evt.Title}");
            }
        }

    }
}
