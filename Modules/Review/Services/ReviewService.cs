using Microsoft.EntityFrameworkCore;
using Order.Interface;
using Review.Interfaces;
using Review.Models;
using Store.Interface;

namespace Review.Services
{
    public class ReviewService : IReviewService
    {
        private readonly ReviewDbContext _context;

        // UserService nam ne treba direktno ovde sada, koristiće se u kontroleru
        // private readonly IUserService _userService;

        private readonly IStoreService _storeService;
        private readonly IOrderService _orderService;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            ReviewDbContext context,
            IStoreService storeService,
            IOrderService orderService,
            ILogger<ReviewService> logger
            )
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<ReviewModel>> GetAllStoreReviewsAsync(int storeId)
        {
            _logger.LogInformation("Fetching approved reviews for StoreId: {StoreId}", storeId);
            var reviews = await _context.Reviews
                .Where(r => r.StoreId == storeId)
                .Include(r => r.Response) // Eager load odgovora ako postoji navigaciona osobina
                .AsNoTracking() // Dobra praksa za read-only upite
                .ToListAsync();

            // Mapiranje u ReviewWithResponse model
            // return reviews.Select(r => new ReviewWithResponse
            // {
            //     Review = r,
            //     Response = r.Response
            // }).ToList();

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

            // return new ReviewWithResponse
            // {
            //     Review = review,
            //     Response = review.Response
            // };

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
        /// Kreira novi odgovor na recenziju.
        /// </summary>
        /// <param name="response">Model odgovora sa podacima.</param>
        /// <returns>Kreirani ReviewResponse sa ID-jem ili null ako validacija ne uspe.</returns>
        public async Task<ReviewResponse?> CreateReviewResponseAsync(ReviewResponse response)
        {
            // --- Validacija ---
            // 1. Provera da li recenzija na koju se odgovara postoji
            bool reviewExists = await _context.Reviews.AnyAsync(r => r.Id == response.ReviewId);
            if (!reviewExists)
            {
                _logger.LogWarning("Failed to create response. ReviewId: {ReviewId} does not exist.", response.ReviewId);
                return null; // Recenzija ne postoji (Not Found)
            }

            // 2. Provera da li odgovor za ovu recenziju već postoji
            bool responseExists = await _context.ReviewResponses.AnyAsync(rr => rr.ReviewId == response.ReviewId);
            if (responseExists)
            {
                _logger.LogWarning("Failed to create response. Response for ReviewId: {ReviewId} already exists.", response.ReviewId);
                return null; // Odgovor već postoji (Konflikt)
            }

            // TODO: Dodati autorizacionu logiku ovde ili pre poziva servisa
            // Proveriti da li korisnik koji pravi odgovor ima pravo na to (npr. vlasnik prodavnice)
            // var review = await _context.Reviews.FindAsync(response.ReviewId);
            // var storeId = review.StoreId;
            // var hasPermission = await _authorizationService.CanRespondToReview(userId, storeId);
            // if (!hasPermission) return null; // Forbidden

            // --- Kraj Validacije ---

            // Postavljanje sistemskih vrednosti
            response.DateTime = DateTime.UtcNow;

            try
            {
                _context.ReviewResponses.Add(response);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Review response created successfully with Id: {ResponseId} for ReviewId: {ReviewId}", response.Id, response.ReviewId);
                return response; // Vraća odgovor sa dodeljenim ID-jem
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving new review response for ReviewId: {ReviewId}", response.ReviewId);
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