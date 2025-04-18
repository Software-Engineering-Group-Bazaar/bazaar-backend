using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Order.Models;
using Users.Models;

namespace Notifications.Services
{
    public class SnsNotificationService
    {
        private readonly IAmazonSimpleNotificationService _snsClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SnsNotificationService> _logger;
        private readonly string? _snsTopicArn;

        public SnsNotificationService(IAmazonSimpleNotificationService snsClient, IConfiguration configuration, ILogger<SnsNotificationService> logger)
        {
            _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Pročitaj ARN topica iz konfiguracije (npr. appsettings.json)
            _snsTopicArn = _configuration["SnsSettings:NewOrderTopicArn"]; // Koristi isto ime kao ranije

            if (string.IsNullOrEmpty(_snsTopicArn))
            {
                _logger.LogError("AWS SNS Topic ARN ('SnsSettings:NewOrderTopicArn') nije konfigurisan.");
                // Razmisli o bacanju izuzetka ovdje ako je Topic obavezan za rad
            }
        }

        // Metoda za slanje notifikacije o novoj narudžbi NA SNS
        public async Task PublishNewOrderNotificationAsync(OrderModel order, User sellerUser)
        {
            if (order == null || sellerUser == null || string.IsNullOrEmpty(_snsTopicArn))
            {
                _logger.LogWarning("Ne mogu poslati SNS notifikaciju o novoj narudžbi - nedostaju podaci ili ARN.");
                return; // Ne šalji ako nema dovoljno podataka ili konfiguracije
            }

            // Kreiraj payload poruke (JSON)
            var messagePayload = new
            {
                NotificationType = "NEW_ORDER", // Tip da subscriber zna šta da radi
                OrderId = order.Id,
                StoreId = order.StoreId,
                OrderDate = order.Time,
                TotalAmount = order.Total,
                SellerId = sellerUser.Id // ID Sellera
            };
            string messageJson = JsonSerializer.Serialize(messagePayload);

            var publishRequest = new PublishRequest
            {
                TopicArn = _snsTopicArn,
                Message = messageJson,
                // Opciono: Atributi za filtriranje
                // MessageAttributes = ...
            };

            try
            {
                _logger.LogInformation("Objavljivanje poruke na SNS Topic {TopicArn} za Novu Narudžbu {OrderId}", _snsTopicArn, order.Id);
                PublishResponse response = await _snsClient.PublishAsync(publishRequest);
                _logger.LogInformation("SNS poruka uspješno objavljena. Message ID: {MessageId}", response.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom objavljivanja poruke na SNS Topic {TopicArn} za Novu Narudžbu {OrderId}", _snsTopicArn, order.Id);
                // Ne bacaj izuzetak da ne prekineš glavni tok
            }
        }

        // Metoda za slanje notifikacije o promjeni statusa NA SNS
        public async Task PublishOrderStatusUpdateAsync(OrderModel order, User buyerUser, OrderStatus newStatus)
        {
            if (order == null || buyerUser == null || string.IsNullOrEmpty(_snsTopicArn))
            {
                _logger.LogWarning("Ne mogu poslati SNS notifikaciju o promjeni statusa - nedostaju podaci ili ARN.");
                return;
            }

            var messagePayload = new
            {
                NotificationType = "ORDER_STATUS_UPDATE",
                OrderId = order.Id,
                NewStatus = newStatus.ToString(),
                BuyerId = buyerUser.Id // ID Buyera
            };
            string messageJson = JsonSerializer.Serialize(messagePayload);

            var publishRequest = new PublishRequest
            {
                TopicArn = _snsTopicArn, // Koristimo isti topic? Ili drugi?
                Message = messageJson,
                // MessageAttributes = ...
            };

            try
            {
                _logger.LogInformation("Objavljivanje poruke na SNS Topic {TopicArn} za Promjenu Statusa Narudžbe {OrderId}", _snsTopicArn, order.Id);
                PublishResponse response = await _snsClient.PublishAsync(publishRequest);
                _logger.LogInformation("SNS poruka o promjeni statusa uspješno objavljena. Message ID: {MessageId}", response.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom objavljivanja poruke o promjeni statusa na SNS Topic {TopicArn} za Narudžbu {OrderId}", _snsTopicArn, order.Id);
            }
        }
    }
}