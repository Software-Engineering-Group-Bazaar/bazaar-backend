using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notifications.Interfaces;
using Review.Interfaces; // Namespace promenjen
using Review.Models; // Potreban za modele koje vraća servis
using Review.Models.DTOs;
using Users.Interface;
using Users.Models; // Namespace promenjen

namespace Review.Controllers // Namespace promenjen
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IUserService _userService; // Dodato za mapiranje ID->Username
        private readonly ILogger<ReviewController> _logger;
        private readonly INotificationService _notificationService;
        private readonly UserManager<User> _userManager;

        private readonly ReviewDbContext _context;

        private readonly IPushNotificationService _pushNotificationService;

        public ReviewController(
            IReviewService reviewService,
            IUserService userService,
            ILogger<ReviewController> logger,
            UserManager<User> userManager,
            INotificationService notificationService,
            IPushNotificationService pushNotificationService,
            ReviewDbContext context)
        {
            _reviewService = reviewService;
            _userService = userService;
            _logger = logger;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
            _context = context;
        }

        // GET api/Review/store/{id}
        [HttpGet("store/{id:int}")]
        [ProducesResponseType(typeof(IEnumerable<ReviewWithResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ReviewWithResponseDto>>> GetStoreReviews(int id)
        {
            var reviews = await _reviewService.GetAllStoreReviewsAsync(id);
            var dtos = new List<ReviewWithResponseDto>();

            foreach (var review in reviews)
            {
                // Mapiranje u DTO unutar kontrolera
                var buyer = await _userService.GetUserWithIdAsync(review.BuyerId);

                var reviewDto = new ReviewDto
                {
                    Id = review.Id,
                    BuyerUsername = (buyer != null && buyer.UserName != null) ? buyer.UserName : "Unknown UserName",
                    StoreId = review.StoreId,
                    OrderId = review.OrderId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    DateTime = review.DateTime,
                    IsApproved = review.IsApproved
                };

                ReviewResponseDto? responseDto = null;
                if (review.Response != null)
                {
                    responseDto = new ReviewResponseDto
                    {
                        Id = review.Response.Id,
                        ReviewId = review.Response.ReviewId,
                        Response = review.Response.Response,
                        DateTime = review.Response.DateTime
                    };
                }

                dtos.Add(new ReviewWithResponseDto { Review = reviewDto, Response = responseDto });
            }

            return Ok(dtos);
        }

        // GET api/Review/store/{id}/approved
        [HttpGet("store/{id:int}/approved")]
        [ProducesResponseType(typeof(IEnumerable<ReviewWithResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ReviewWithResponseDto>>> GetStoreApprovedReviews(int id)
        {
            var reviews = await _reviewService.GetStoreApprovedReviewsAsync(id);
            var dtos = new List<ReviewWithResponseDto>();

            foreach (var review in reviews)
            {
                // Mapiranje u DTO unutar kontrolera
                var buyer = await _userService.GetUserWithIdAsync(review.BuyerId);

                var reviewDto = new ReviewDto
                {
                    Id = review.Id,
                    BuyerUsername = (buyer != null && buyer.UserName != null) ? buyer.UserName : "Unknown UserName",
                    StoreId = review.StoreId,
                    OrderId = review.OrderId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    DateTime = review.DateTime,
                    IsApproved = review.IsApproved
                };

                ReviewResponseDto? responseDto = null;
                if (review.Response != null)
                {
                    responseDto = new ReviewResponseDto
                    {
                        Id = review.Response.Id,
                        ReviewId = review.Response.ReviewId,
                        Response = review.Response.Response,
                        DateTime = review.Response.DateTime
                    };
                }

                dtos.Add(new ReviewWithResponseDto { Review = reviewDto, Response = responseDto });
            }

            return Ok(dtos);
        }

        // GET api/Review/store/{id}/rating
        [HttpGet("store/{id:int}/rating")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // Vraća objekat { rating: number | null }
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> GetStoreRating(int id)
        {
            // Nema mapiranja, servis već vraća double?
            var averageRating = await _reviewService.GetStoreAverageRatingAsync(id);

            // Možemo vratiti objekat da JSON bude konzistentan `{ "rating": 4.5 }` ili `{ "rating": null }`
            // Vraćanje direktno `null` može rezultirati praznim 200 OK odgovorom, što nije idealno.
            // Vraćanje NotFound ako je null je takođe opcija. Odlučujemo se za Ok sa objektom.
            if (averageRating == null)
            {
                _logger.LogInformation("No approved reviews found for store {StoreId} to calculate rating.", id);
            }
            return Ok(new { rating = averageRating });

            // Alternativa: NotFound ako nema ocene
            // if (averageRating == null)
            // {
            //     _logger.LogInformation("No approved reviews found for store {StoreId} to calculate rating.", id);
            //     return NotFound($"No rating available for store {id}.");
            // }
            // return Ok(new { rating = averageRating.Value });
        }

        // GET api/Review/order/{id}
        [HttpGet("order/{id:int}")]
        [ProducesResponseType(typeof(ReviewWithResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReviewWithResponseDto>> GetOrderReview(int id)
        {
            var review = await _reviewService.GetOrderReviewAsync(id);

            if (review == null)
            {
                _logger.LogInformation("Approved review for order {OrderId} not found.", id);
                return NotFound($"Approved review for order {id} not found.");
            }

            var responseModel = review.Response;

            // Mapiranje u DTO
            var buyer = await _userService.GetUserWithIdAsync(review.BuyerId);

            var reviewDto = new ReviewDto
            {
                Id = review.Id,
                BuyerUsername = (buyer != null && buyer.UserName != null) ? buyer.UserName : "Unknown UserName",
                StoreId = review.StoreId,
                OrderId = review.OrderId,
                Rating = review.Rating,
                Comment = review.Comment,
                DateTime = review.DateTime,
                IsApproved = review.IsApproved
            };

            ReviewResponseDto? responseDto = null;
            if (responseModel != null)
            {
                responseDto = new ReviewResponseDto
                {
                    Id = responseModel.Id,
                    ReviewId = responseModel.ReviewId,
                    Response = responseModel.Response,
                    DateTime = responseModel.DateTime
                };
            }

            var resultDto = new ReviewWithResponseDto { Review = reviewDto, Response = responseDto };
            return Ok(resultDto);
        }

        // POST api/Review/
        [HttpPost]
        [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)] // Vraća DTO
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewRequestDto requestDto)
        {
            var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(buyerId)) return Unauthorized("User identifier not found.");

            _logger.LogInformation("User {BuyerId} attempting to create review for order {OrderId}", buyerId, requestDto.OrderId);

            var request = new ReviewModel
            {
                Id = 0,
                BuyerId = buyerId,
                StoreId = requestDto.StoreId,
                OrderId = requestDto.OrderId,
                Rating = requestDto.Rating,
                Comment = requestDto.Comment,
                DateTime = DateTime.Now,
                IsApproved = true
            };


            // Servis sada vraća model ili null
            var createdReviewModel = await _reviewService.CreateReviewAsync(request);

            if (createdReviewModel == null)
            {
                // Razlog za null može biti validacija (npr. store ne postoji) ili konflikt (review već postoji)
                // Potrebna je dodatna logika ili bolji signal iz servisa da se razlikuje 400 od 409
                // Za sada, pretpostavimo da je null uglavnom zbog konflikta ili neuspele validacije
                _logger.LogWarning("Failed to create review for order {OrderId} by user {BuyerId}. Service returned null.", requestDto.OrderId, buyerId);
                // Možemo vratiti Conflict kao verovatniji slučaj
                return Conflict(new { message = $"Failed to create review. It might already exist, or associated store/order is invalid." });
            }

            var buyer = await _userService.GetUserWithIdAsync(createdReviewModel.BuyerId);

            var reviewDto = new ReviewDto
            {
                Id = createdReviewModel.Id,
                BuyerUsername = (buyer != null && buyer.UserName != null) ? buyer.UserName : "Unknown UserName",
                StoreId = createdReviewModel.StoreId,
                OrderId = createdReviewModel.OrderId,
                Rating = createdReviewModel.Rating,
                Comment = createdReviewModel.Comment,
                DateTime = createdReviewModel.DateTime,
                IsApproved = createdReviewModel.IsApproved
            };

            var sellerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == createdReviewModel.StoreId);

            if (sellerUser != null)
            {
                string notificationMessage = $"Nova recenzija #{createdReviewModel.Id} je kreirana za vašu prodavnicu.";

                await _notificationService.CreateNotificationAsync(
                        sellerUser.Id,
                        notificationMessage,
                        createdReviewModel.Id
                    );
                _logger.LogInformation("Notification creation task initiated for Seller {SellerUserId} for new Order {ReviewId}.", sellerUser.Id, createdReviewModel.Id);

                if (!string.IsNullOrWhiteSpace(sellerUser.FcmDeviceToken))
                {
                    try
                    {
                        string pushTitle = "Nova Recenzija!";
                        string pushBody = $"Dobili ste recenziju #{createdReviewModel.Id}.";
                        var pushData = new Dictionary<string, string> {
                        { "reviewId", createdReviewModel.Id.ToString() },
                        { "screen", "ReviewDetail" }
                    };

                        await _pushNotificationService.SendPushNotificationAsync(
                            sellerUser.FcmDeviceToken,
                            pushTitle,
                            pushBody,
                            pushData
                        );
                        _logger.LogInformation("Push Notification task initiated for Seller {SellerUserId} for new review {ReviewId}.", sellerUser.Id, createdReviewModel.Id);
                    }
                    catch (Exception pushEx)
                    {
                        _logger.LogError(pushEx, "Failed to send Push Notification to Seller {SellerUserId} for Order {ReviewId}.", sellerUser.Id, createdReviewModel.Id);
                    }
                }

            }
            else
            {
                _logger.LogWarning("Could not find seller user for StoreId {StoreId} to send new review notification for review {ReviewId}.", createdReviewModel.StoreId, createdReviewModel.Id);
            }

            _logger.LogInformation("Review {ReviewId} created successfully for review {ReviewId} by user {BuyerId}", createdReviewModel.Id, requestDto.OrderId, buyerId);
            return CreatedAtAction(nameof(GetOrderReview), new { id = reviewDto.OrderId }, reviewDto);
        }

        // POST api/Review/response
        [HttpPost("response")]
        [Authorize(Roles = "Seller")] // <<< Samo Seller može odgovoriti
        [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako Seller nije vlasnik prodavnice recenzije
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako recenzija ne postoji
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Ako odgovor već postoji
        public async Task<ActionResult<ReviewResponseDto>> CreateReviewResponse([FromBody] CreateReviewResponseRequestDto requestDto)
        {
            // 1. Dohvati ID ulogovanog korisnika (Sellera)
            var sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sellerId))
            {
                _logger.LogWarning("CreateReviewResponse: Seller ID claim not found in token.");
                return Unauthorized("User identifier not found.");
            }

            _logger.LogInformation("Seller {SellerId} attempting to create response for review {ReviewId}", sellerId, requestDto.ReviewId);

            // 2. Validacija ulaznog DTO-a
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateReviewResponse for ReviewId {ReviewId} by Seller {SellerId} failed model validation. Errors: {@ModelState}",
                    requestDto.ReviewId, sellerId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }
            if (requestDto.ReviewId <= 0) return BadRequest("Invalid Review ID.");
            if (string.IsNullOrWhiteSpace(requestDto.Response)) return BadRequest("Response text cannot be empty.");


            try
            {
                // 3. Provjeri da li recenzija postoji i da li Seller ima pravo da odgovori
                var originalReview = await _reviewService.GetReviewByIdAsync(requestDto.ReviewId);
                if (originalReview == null)
                {
                    _logger.LogWarning("Failed to create response by Seller {SellerId}: Original Review {ReviewId} not found.", sellerId, requestDto.ReviewId);
                    return NotFound($"Review with ID {requestDto.ReviewId} not found.");
                }

                // Autorizacija: Da li je Seller vlasnik prodavnice za koju je recenzija?
                var sellerUser = await _userManager.FindByIdAsync(sellerId); // Potrebno je injektirati UserManager<User> _userManager
                if (sellerUser == null)
                {
                    _logger.LogError("Authenticated Seller {SellerId} not found in database.", sellerId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Authenticated user not found.");
                }
                if (!sellerUser.StoreId.HasValue || sellerUser.StoreId.Value != originalReview.StoreId)
                {
                    _logger.LogWarning("Forbidden: Seller {SellerId} (Store: {SellerStoreId}) attempted to respond to Review {ReviewId} for Store {ReviewStoreId}.",
                        sellerId, sellerUser.StoreId?.ToString() ?? "None", requestDto.ReviewId, originalReview.StoreId);
                    return Forbid("You are not authorized to respond to this review.");
                }
                _logger.LogInformation("Seller {SellerId} authorized to respond to Review {ReviewId}.", sellerId, requestDto.ReviewId);


                // 4. Kreiraj ReviewResponse objekat
                var reviewResponseToCreate = new ReviewResponse
                {
                    Id = 0, // EF Core će generisati
                    ReviewId = requestDto.ReviewId,
                    Response = requestDto.Response,
                    DateTime = DateTime.UtcNow // Postavi vrijeme ovdje ili u servisu
                                               // SellerId se može dodati u ReviewResponse model ako želiš pratiti ko je odgovorio,
                                               // ali za sada se oslanjamo na autorizaciju.
                };

                // 5. Pozovi servisnu metodu sa oba argumenta
                var createdResponseModel = await _reviewService.CreateReviewResponseAsync(reviewResponseToCreate, sellerId);

                if (createdResponseModel == null)
                {
                    // Servis vraća null ako odgovor već postoji ili ako je originalna recenzija obrisana u međuvremenu
                    _logger.LogWarning("Failed to create response for review {ReviewId} by Seller {SellerId}. Service returned null (likely response already exists or review deleted).", requestDto.ReviewId, sellerId);
                    // Provjeri ponovo da li odgovor postoji da bi vratio Conflict umjesto generičke greške
                    bool responseNowExists = await _context.ReviewResponses.AnyAsync(rr => rr.ReviewId == requestDto.ReviewId); // Treba ti _context (ReviewDbContext) ovdje
                    if (responseNowExists) return Conflict(new { message = $"A response for review {requestDto.ReviewId} already exists." });
                    return BadRequest("Failed to create review response. Please try again."); // Generalna greška ako nije Conflict
                }

                // 6. Mapiranje kreiranog modela u DTO za odgovor klijentu
                var createdResponseDto = new ReviewResponseDto
                {
                    Id = createdResponseModel.Id,
                    ReviewId = createdResponseModel.ReviewId,
                    Response = createdResponseModel.Response,
                    DateTime = createdResponseModel.DateTime
                };

                _logger.LogInformation("Response {ResponseId} created successfully for review {ReviewId} by Seller {SellerId}", createdResponseDto.Id, requestDto.ReviewId, sellerId);
                // Vraćamo 200 OK sa DTO-om jer se ne kreira novi resurs sa sopstvenim URL-om za GET by ID
                // Ako bi imao GET /api/Review/response/{responseId}, onda bi koristio CreatedAtAction
                return Ok(createdResponseDto);
            }
            catch (UnauthorizedAccessException ex) // Uhvaćeno ako servis baci (iako smo provjerili ovdje)
            {
                _logger.LogWarning(ex, "Unauthorized attempt by Seller {SellerId} to respond to Review {ReviewId}.", sellerId, requestDto.ReviewId);
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex) // Npr. ako DTO nije validan ili ReviewId <= 0
            {
                _logger.LogWarning(ex, "Argument error creating review response for ReviewId {ReviewId} by Seller {SellerId}.", requestDto.ReviewId, sellerId);
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex) // Ako UserManager ne nađe Sellera
            {
                _logger.LogWarning(ex, "KeyNotFound error creating review response for ReviewId {ReviewId} by Seller {SellerId}.", requestDto.ReviewId, sellerId);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex) // Sve ostale greške
            {
                _logger.LogError(ex, "An error occurred while creating review response for ReviewId {ReviewId} by Seller {SellerId}.", requestDto.ReviewId, sellerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the review response.");
            }
        }
    }

}