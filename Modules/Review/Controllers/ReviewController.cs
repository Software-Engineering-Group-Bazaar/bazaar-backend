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

        private readonly IPushNotificationService _pushNotificationService;

        public ReviewController(
            IReviewService reviewService,
            IUserService userService,
            ILogger<ReviewController> logger,
            UserManager<User> userManager,
            INotificationService notificationService,
            IPushNotificationService pushNotificationService)
        {
            _reviewService = reviewService;
            _userService = userService;
            _logger = logger;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
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
        [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status201Created)] // Vraća DTO
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ReviewResponseDto>> CreateReviewResponse([FromBody] CreateReviewResponseRequestDto requestDto)
        {
            // TODO: Autorizacija - provera da li ulogovani korisnik sme da odgovori
            // var sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier); ...

            _logger.LogInformation("Attempting to create response for review {ReviewId}", requestDto.ReviewId);

            // Prvo proverimo da li recenzija postoji da bismo vratili korektan NotFound
            var reviewExists = await _reviewService.GetReviewByIdAsync(requestDto.ReviewId);
            if (reviewExists == null)
            {
                _logger.LogWarning("Failed to create response - Review {ReviewId} not found.", requestDto.ReviewId);
                return NotFound($"Review with ID {requestDto.ReviewId} not found.");
            }

            var request = new ReviewResponse
            {
                Id = 0,
                ReviewId = requestDto.ReviewId,
                Response = requestDto.Response,
                DateTime = DateTime.Now
            };

            // Servis vraća model ili null
            var createdResponseModel = await _reviewService.CreateReviewResponseAsync(request);

            if (createdResponseModel == null)
            {
                // Ako recenzija postoji, null iz servisa sada verovatno znači da odgovor već postoji
                _logger.LogWarning("Failed to create response for review {ReviewId}. Response might already exist.", requestDto.ReviewId);
                return Conflict(new { message = $"A response for review {requestDto.ReviewId} might already exist." });
            }

            // Mapiranje kreiranog modela u DTO
            var createdResponseDto = new ReviewResponseDto
            {
                Id = createdResponseModel.Id,
                ReviewId = createdResponseModel.ReviewId,
                Response = createdResponseModel.Response,
                DateTime = createdResponseModel.DateTime
            };

            _logger.LogInformation("Response {ResponseId} created successfully for review {ReviewId}", createdResponseDto.Id, requestDto.ReviewId);
            // Vraćamo DTO. Može Ok ili CreatedAtAction ako postoji GET endpoint za response po ID.
            return Ok(createdResponseDto);
        }
    }

}