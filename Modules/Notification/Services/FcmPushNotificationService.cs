using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;

namespace Notifications.Services
{
    public class FcmPushNotificationService : IPushNotificationService
    {
        private readonly ILogger<FcmPushNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private bool _firebaseAppInitialized = false;

        // Konstruktor
        public FcmPushNotificationService(IConfiguration configuration, ILogger<FcmPushNotificationService> logger, IHostEnvironment env)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (FirebaseApp.DefaultInstance == null)
            {
                try
                {
                    // Pročitaj putanju do Firebase Admin SDK JSON ključa iz appsettings
                    string? firebaseCredentialsPath = _configuration["Firebase:AdminSdkCredentialsPath"];

                    if (string.IsNullOrWhiteSpace(firebaseCredentialsPath))
                    {
                        _logger.LogError("Firebase Admin SDK credentials path ('Firebase:AdminSdkCredentialsPath') is not configured in appsettings.");
                        return; // Ne možemo nastaviti bez putanje
                    }

                    // Ako je putanja relativna, kombinuj je sa ContentRootPath
                    // Ovo omogućava da putanja bude npr. "firebase-adminsdk.json" i da se fajl nalazi u root-u projekta
                    if (!Path.IsPathRooted(firebaseCredentialsPath))
                    {
                        firebaseCredentialsPath = Path.Combine(env.ContentRootPath, firebaseCredentialsPath);
                    }


                    if (!File.Exists(firebaseCredentialsPath))
                    {
                        _logger.LogError("Firebase Admin SDK credentials file not found at path: {Path}", firebaseCredentialsPath);
                        return; // Ne možemo nastaviti bez fajla
                    }

                    _logger.LogInformation("Initializing Firebase Admin SDK with credentials from: {Path}", firebaseCredentialsPath);

                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(firebaseCredentialsPath),
                        // Opciono: ProjectId možeš postaviti ako SDK ne može sam detektovati
                        // ProjectId = _configuration["Firebase:ProjectId"]
                    });

                    _firebaseAppInitialized = true;
                    _logger.LogInformation("Firebase Admin SDK initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Firebase Admin SDK.");
                    // _firebaseAppInitialized ostaje false
                }
            }
            else
            {
                _firebaseAppInitialized = true; // Već je inicijalizovano ranije
                _logger.LogInformation("Firebase Admin SDK was already initialized.");
            }
        }

        // Metoda za slanje
        public async Task<bool> SendPushNotificationAsync(string userFcmToken, string title, string body, Dictionary<string, string>? data = null)
        {
            // Provjeri da li je SDK inicijalizovan i da li imamo token
            if (!_firebaseAppInitialized)
            {
                _logger.LogError("Cannot send push notification because Firebase Admin SDK failed to initialize.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(userFcmToken))
            {
                _logger.LogWarning("Cannot send push notification: FCM Device Token is missing for the target user.");
                return false;
            }

            _logger.LogInformation("Attempting to send push notification to token {Token} - Title: {Title}", userFcmToken, title);

            // Kreiraj Firebase poruku
            var message = new Message()
            {
                // --- Android Konfiguracija ---
                // Možeš dodati specifična podešavanja za Android ovdje ako treba
                // Android = new AndroidConfig { Notification = new AndroidNotification { ... } },

                // --- Apple (APNS) Konfiguracija ---
                // Možeš dodati specifična podešavanja za iOS ovdje ako treba
                // Apns = new ApnsConfig { Aps = new Aps { Alert = new ApsAlert { ... } } },

                // --- Osnovna Notifikacija (koristit će je obje platforme ako specifična nije data) ---
                Notification = new Notification
                {
                    Title = title,
                    Body = body,
                },

                // --- Dodatni Podaci (Data Payload) ---
                // Ovi podaci se šalju aplikaciji čak i ako je u pozadini.
                // Frontend ih može koristiti da npr. otvori određeni ekran kada se klikne na notifikaciju.
                Data = data, // Proslijeđeni dictionary

                // --- Ciljni Uređaj ---
                Token = userFcmToken // FCM token specifičnog uređaja
                // Možeš slati i na Topic (ako korisnici subscribuju na topice u app) ili na Condition
                // Topic = "ime-topica"
            };

            try
            {
                // Pošalji poruku koristeći Firebase Messaging
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Successfully sent push notification. FCM Message ID: {FcmMessageId}", response);
                return true;
            }
            catch (FirebaseMessagingException ex)
            {
                // Detaljnije hvatanje grešaka od Firebase-a
                _logger.LogError(ex, "Firebase Messaging error sending push notification to token {Token}. MessagingErrorCode: {ErrorCode}", userFcmToken, ex.MessagingErrorCode);
                // Ovdje možeš provjeriti ex.MessagingErrorCode za specifične greške kao 'UNREGISTERED' (token više ne važi) ili 'INVALID_ARGUMENT'
                if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    _logger.LogWarning("FCM token {Token} is invalid or unregistered. Consider removing it from the database.", userFcmToken);
                    // TODO: Opciono dodati logiku za brisanje nevažećeg tokena iz baze
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error sending push notification to token {Token}", userFcmToken);
                return false;
            }
        }
    }
}