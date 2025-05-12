using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Notifications.Interfaces;
using Order.Interface;
using Review.Interfaces;
using Review.Models;
using Store.Interface;
using Users.Models;

namespace Review.Services
{
    public class ReviewService : IReviewService
    {
        private readonly ReviewDbContext _context;

        // UserService nam ne treba direktno ovde sada, koristiće se u kontroleru
        // private readonly IUserService _userService;

        private readonly IStoreService _storeService;
        private readonly IOrderService _orderService;
        private readonly UserManager<User> _userManager;
        private readonly INotificationService _dbNotificationService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            ReviewDbContext context,
            IStoreService storeService,
            UserManager<User> userManager,
            IOrderService orderService,
            INotificationService dbNotificationService,
            IPushNotificationService pushNotificationService,
            ILogger<ReviewService> logger
            )
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _dbNotificationService = dbNotificationService ?? throw new ArgumentNullException(nameof(dbNotificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        }

        public async Task<IEnumerable<ReviewModel>> GetAllStoreReviewsAsync(int storeId)
        {
            _logger.LogInformation("Fetching approved reviews for StoreId: {StoreId}", storeId);
            var reviews = await _context.Reviews
                .Where(r => r.StoreId == storeId)
                .Include(r => r.Response) // Eager load odgovora ako postoji navigaciona osobina
                .AsNoTracking() // Dobra praksa za read-only upite
                .ToListAsync();

            return reviews;
        }

        public async Task<IEnumerable<ReviewModel>> GetStoreApprovedReviewsAsync(int storeId)
        {
            _logger.LogInformation("Fetching approved reviews for StoreId: {StoreId}", storeId);
            var reviews = await _context.Reviews
                .Where(r => r.StoreId == storeId && r.IsApproved)
                .Include(r => r.Response) // Eager load odgovora ako postoji navigaciona osobina
                .AsNoTracking() // Dobra praksa za read-only upite
                .ToListAsync();

            return reviews;
        }

        /// <summary>
        /// Dobija odobrenu recenziju za specifičnu porudžbinu, uključujući odgovor.
        /// </summary>
        public async Task<ReviewModel?> GetOrderReviewAsync(int orderId)
        {
            _logger.LogInformation("Fetching approved review for OrderId: {OrderId}", orderId);
            var review = await _context.Reviews
                .Where(r => r.OrderId == orderId)
                .Include(r => r.Response) // Eager load odgovora
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (review == null)
            {
                _logger.LogInformation("Approved review for OrderId: {OrderId} not found.", orderId);
                return null;
            }

            return review;
        }

        /// <summary>
        /// Izračunava prosečnu ocenu za prodavnicu na osnovu odobrenih recenzija.
        /// </summary>
        public async Task<double?> GetStoreAverageRatingAsync(int storeId)
        {
            _logger.LogInformation("Calculating average rating for StoreId: {StoreId}", storeId);
            var query = _context.Reviews
                .Where(r => r.StoreId == storeId && r.IsApproved);

            // Prvo proverimo da li uopšte ima odobrenih recenzija
            bool hasReviews = await query.AnyAsync();
            if (!hasReviews)
            {
                _logger.LogInformation("No approved reviews found for StoreId: {StoreId} to calculate rating.", storeId);
                return null;
            }

            // Izračunaj prosek
            double average = await query.AverageAsync(r => r.Rating);
            _logger.LogInformation("Average rating for StoreId: {StoreId} is {AverageRating}", storeId, average);
            return average;
        }

        /// <summary>
        /// Kreira novu recenziju.
        /// </summary>
        /// <param name="review">Model recenzije sa podacima.</param>
        /// <returns>Kreirani ReviewModel sa ID-jem ili null ako validacija ne uspe.</returns>
        public async Task<ReviewModel?> CreateReviewAsync(ReviewModel review)
        {
            // --- Validacija ---
            // 1. Provera da li prodavnica postoji (koristeći IStoreService ili direktan upit)
            // Primer direktnog upita:
            // bool storeExists = await _context.Stores.AnyAsync(s => s.Id == review.StoreId && s.IsActive);
            // Primer korišćenja servisa:
            var store = _storeService.GetStoreById(review.StoreId); // Pretpostavljena metoda
            if (store == null || !store.isActive)
            {
                _logger.LogWarning("Failed to create review. StoreId: {StoreId} does not exist or is inactive.", review.StoreId);
                return null; // Prodavnica ne postoji ili nije aktivna
            }

            // 2. Provera da li porudžbina postoji i pripada kupcu (koristeći IOrderService)
            // Primer korišćenja servisa:
            var order = await _orderService.GetOrderByIdAsync(review.OrderId); // Pretpostavljena metoda
            if (order == null || order.BuyerId != review.BuyerId)
            {
                _logger.LogWarning("Failed to create review. OrderId: {OrderId} is invalid or does not belong to BuyerId: {BuyerId}.", review.OrderId, review.BuyerId);
                return null; // Porudžbina nije validna
            }

            // 3. Provera da li recenzija za ovu porudžbinu već postoji
            bool reviewExists = await _context.Reviews.AnyAsync(r => r.OrderId == review.OrderId);
            if (reviewExists)
            {
                _logger.LogWarning("Failed to create review. Review for OrderId: {OrderId} already exists.", review.OrderId);
                return null; // Recenzija već postoji (Konflikt)
            }
            // --- Kraj Validacije ---


            // Postavljanje sistemskih vrednosti
            review.DateTime = DateTime.UtcNow;
            review.IsApproved = true; // Nove recenzije čekaju odobrenje

            try
            {
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Review created successfully with Id: {ReviewId} for OrderId: {OrderId}", review.Id, review.OrderId);
                return review; // Vraća recenziju sa dodeljenim ID-jem
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving new review for OrderId: {OrderId}", review.OrderId);
                // Možda je došlo do retkog race condition-a ili drugog DB problema
                return null;
            }
        }

        /// <summary>
        /// Kreira novi odgovor na recenziju i šalje notifikaciju kupcu.
        /// </summary>
        /// <param name="response">ReviewResponse objekat sa podacima za odgovor (ReviewId, Response text).</param>
        /// <param name="sellerUserId">ID Sellera koji kreira odgovor (radi autorizacije).</param>
        /// <returns>Kreirani ReviewResponse objekat ili null ako dođe do greške ili validacija ne uspije.</returns>
        public async Task<ReviewResponse?> CreateReviewResponseAsync(ReviewResponse response, string sellerUserId)
        {
            _logger.LogInformation("Attempting to create response for ReviewId: {ReviewId} by SellerId: {SellerId}", response.ReviewId, sellerUserId);

            // --- Osnovna Validacija Ulaznih Podataka ---
            if (response == null)
            {
                _logger.LogWarning("CreateReviewResponseAsync called with null response object.");
                throw new ArgumentNullException(nameof(response));
            }
            if (string.IsNullOrWhiteSpace(sellerUserId))
            {
                _logger.LogWarning("CreateReviewResponseAsync called with null or empty sellerUserId.");
                throw new ArgumentNullException(nameof(sellerUserId));
            }
            if (response.ReviewId <= 0)
            {
                _logger.LogWarning("CreateReviewResponseAsync: Invalid ReviewId {ReviewId} provided.", response.ReviewId);
                throw new ArgumentException("Invalid ReviewId provided.", nameof(response.ReviewId));
            }
            if (string.IsNullOrWhiteSpace(response.Response))
            {
                _logger.LogWarning("CreateReviewResponseAsync: Response text cannot be empty for ReviewId {ReviewId}.", response.ReviewId);
                throw new ArgumentException("Response text cannot be empty.", nameof(response.Response));
            }

            // 1. Dohvati originalnu recenziju da bismo imali BuyerId i StoreId
            var originalReview = await _context.Reviews.AsNoTracking()
                                           .FirstOrDefaultAsync(r => r.Id == response.ReviewId);
            if (originalReview == null)
            {
                _logger.LogWarning("Failed to create response. Original Review with ID: {ReviewId} does not exist.", response.ReviewId);
                return null; // Recenzija ne postoji
            }

            // 2. AUTORIZACIJA: Proveri da li ulogovani Seller (sellerUserId) smije da odgovori
            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null)
            {
                _logger.LogError("Seller user with ID {SellerId} (who is trying to respond) not found.", sellerUserId);
                throw new KeyNotFoundException($"Seller user with ID {sellerUserId} not found."); // Ili vrati null/Forbid
            }
            if (!sellerUser.StoreId.HasValue || sellerUser.StoreId.Value != originalReview.StoreId)
            {
                _logger.LogWarning("Seller {SellerId} is not authorized to respond to Review {ReviewId} for Store {StoreId}. Seller's store is {SellerStoreId}.",
                                  sellerUserId, response.ReviewId, originalReview.StoreId, sellerUser.StoreId?.ToString() ?? "None");
                throw new UnauthorizedAccessException("You are not authorized to respond to this review.");
            }
            _logger.LogInformation("Seller {SellerId} authorized to respond to Review {ReviewId}.", sellerUserId, response.ReviewId);


            bool responseExists = await _context.ReviewResponses.AnyAsync(rr => rr.ReviewId == response.ReviewId);
            if (responseExists)
            {
                _logger.LogWarning("Failed to create response. Response for ReviewId: {ReviewId} already exists.", response.ReviewId);
                // Vrati null ili baci izuzetak da kontroler može vratiti Conflict
                // throw new InvalidOperationException($"A response for review ID {response.ReviewId} already exists.");
                return null;
            }

            // Postavljanje sistemskih vrednosti za novi ReviewResponse objekat
            response.Id = 0; // Osiguraj da EF Core kreira novi ID
            response.DateTime = DateTime.UtcNow;
            // response.ReviewId je već postavljen iz ulaznog objekta
            // response.Review navigaciono svojstvo se ne postavlja ovdje, EF Core to radi po potrebi

            try
            {
                _context.ReviewResponses.Add(response);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Review response created successfully with Id: {ResponseId} for ReviewId: {ReviewId} by Seller {SellerId}",
                                       response.Id, response.ReviewId, sellerUserId);
                try
                {
                    // originalReview.BuyerId je ID kupca koji je ostavio originalnu recenziju
                    var buyerToNotify = await _userManager.FindByIdAsync(originalReview.BuyerId);
                    if (buyerToNotify != null)
                    {
                        // Dohvati ime prodavnice radi konteksta u notifikaciji
                        var store = _storeService.GetStoreById(originalReview.StoreId); // Koristi async
                        string storeNameForNotification = store?.name ?? "prodavnice"; // Koristi 'name' iz StoreModela

                        string notificationMessage = $"Seller iz '{storeNameForNotification}' je odgovorio na Vašu recenziju (Narudžba #{originalReview.OrderId}).";
                        string pushTitle = "Odgovor na Vašu Recenziju";

                        // 1. DB Notifikacija za Buyera
                        // Tvoj INotificationService.CreateNotificationAsync prima (userId, message, optional_orderId)
                        // Koristimo ID odgovora (response.Id) kao "relatedEntityId" za notifikaciju,
                        // jer je to novi entitet koji je relevantan za Buyera.
                        await _dbNotificationService.CreateNotificationAsync(
                            buyerToNotify.Id,
                            notificationMessage,
                            response.Id // Poveži notifikaciju sa ID-jem odgovora
                        );
                        _logger.LogInformation("DB Notification creation initiated for Buyer {BuyerId} for new ReviewResponse {ResponseId} on Review {ReviewId}.",
                                               buyerToNotify.Id, response.Id, originalReview.Id);

                        // 2. Push Notifikacija za Buyera
                        if (!string.IsNullOrWhiteSpace(buyerToNotify.FcmDeviceToken))
                        {
                            // Pretpostavka: platforma je Android jer samo FcmDeviceToken postoji na User modelu
                            var pushData = new Dictionary<string, string> {
                                { "reviewResponseId", response.Id.ToString() }, // ID odgovora
                                { "originalReviewId", originalReview.Id.ToString() },
                                { "orderId", originalReview.OrderId.ToString() }, // Ako je relevantno
                                { "screen", "ReviewDetails" } // Primjer ekrana na koji treba navigirati
                            };

                            try
                            {
                                await _pushNotificationService.SendPushNotificationAsync(
                                    buyerToNotify.FcmDeviceToken,
                                    pushTitle,
                                    notificationMessage, // Ili skraćena verzija za push
                                    pushData
                                );
                                _logger.LogInformation("Push Notification task initiated for Buyer {BuyerId} for new ReviewResponse {ResponseId}.",
                                                       buyerToNotify.Id, response.Id);
                            }
                            catch (Exception pushEx)
                            {
                                _logger.LogError(pushEx, "Failed to send Push Notification to Buyer {BuyerId} for new ReviewResponse {ResponseId}.",
                                                 buyerToNotify.Id, response.Id);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Buyer {BuyerId} does not have an FcmDeviceToken for review response notification (ReviewResponseId: {ResponseId}).",
                                               buyerToNotify.Id, response.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find Buyer (ID: {OriginalBuyerId}) from original review to send notification for response on Review {ReviewId}.",
                                           originalReview.BuyerId, originalReview.Id);
                    }
                }
                catch (Exception notifyEx)
                {
                    // Loguj grešku slanja notifikacija, ali ne prekidaj glavni tok jer je odgovor sačuvan
                    _logger.LogError(notifyEx, "Error sending notifications to Buyer for response on Review {ReviewId}.", originalReview.Id);
                }

                return response; // Vraća sačuvani ReviewResponse objekat
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving new review response for ReviewId: {ReviewId}", response.ReviewId);
                // Razmisli o bacanju specifičnijeg izuzetka ako DbUpdateException
                // ukazuje na npr. unique constraint violation koji AnyAsync nije uhvatio
                return null;
            }
        }

        /// <summary>
        /// Dobija recenziju po njenom ID-ju.
        /// </summary>
        public async Task<ReviewModel?> GetReviewByIdAsync(int reviewId)
        {
            _logger.LogInformation("Fetching review by Id: {ReviewId}", reviewId);
            // FindAsync je optimizovan za traženje po primarnom ključu
            var review = await _context.Reviews.Include(r => r.Response).FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
            {
                _logger.LogInformation("Review with Id: {ReviewId} not found.", reviewId);
            }
            return review;
        }


    }
}