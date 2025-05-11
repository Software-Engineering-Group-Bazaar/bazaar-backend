using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Users.Dtos;
using Users.Interface;
using Users.Interfaces;
using Users.Models.Dtos;

namespace Users.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleSignInService _googleSignInService;
        private readonly IFacebookSignInService _facebookSignInService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IGoogleSignInService googleSignInService,
            IFacebookSignInService facebookSignInService,
            ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _googleSignInService = googleSignInService ?? throw new ArgumentNullException(nameof(googleSignInService));
            _facebookSignInService = facebookSignInService ?? throw new ArgumentNullException(nameof(facebookSignInService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<string>> Register([FromBody] RegisterDto registerDto)
        {
            _logger.LogInformation("Registration attempt for email: {Email}, username: {Username}, app: {App}",
                                   registerDto.Email, registerDto.Username, registerDto.App);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Registration failed model validation for email: {Email}. Errors: {ValidationErrors}",
                                   registerDto.Email, string.Join(", ", errors));
                return BadRequest(new { message = "Validation failed.", errors = ModelState });
            }

            try
            {
                var result = await _authService.RegisterAsync(registerDto);
                if (result.Contains("uspješna") || result.Contains("successful"))
                {
                    _logger.LogInformation("Registration successful for email: {Email}, username: {Username}",
                                           registerDto.Email, registerDto.Username);
                    return Ok(new { message = result });
                }
                else
                {
                    _logger.LogWarning("Registration failed for email: {Email}, username: {Username}. Reason: {Reason}",
                                       registerDto.Email, registerDto.Username, result);
                    return BadRequest(new { message = result });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for email: {Email}", registerDto.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during registration." });
            }
        }

        // POST api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            _logger.LogInformation("Login attempt for email: {Email}, app: {App}", loginDto.Email, loginDto.App);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Login failed model validation for email: {Email}. Errors: {ValidationErrors}",
                                   loginDto.Email, string.Join(", ", errors));
                return BadRequest(new { message = "Validation failed.", errors = ModelState });
            }

            try
            {
                var response = await _authService.LoginAsync(loginDto);
                if (response == null)
                {
                    _logger.LogWarning("Login failed for email: {Email}. Invalid credentials or user not found/approved.", loginDto.Email);
                    return Unauthorized(new { message = "Neispravni podaci ili korisnik nije odobren." });
                }

                _logger.LogInformation("Login successful for email: {Email}. Token issued.", loginDto.Email);
                return Ok(response);

            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Login failed for email: {Email} due to invalid operation (e.g., account locked, not approved).", loginDto.Email);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for email: {Email}", loginDto.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during login." });
            }
        }

        // POST api/auth/logout
        [HttpPost("logout")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("User {UserId} attempting to logout.", userId ?? "Unknown (no ID claim)");

            try
            {
                await _authService.LogoutAsync();
                _logger.LogInformation("User {UserId} successfully logged out.", userId ?? "Unknown");
                return Ok(new { message = "Izlogovani ste." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for User {UserId}.", userId ?? "Unknown");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during logout." });
            }
        }

        // POST api/auth/login/google
        [AllowAnonymous]
        [HttpPost("login/google")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponseDto>> SignGoogleUser([FromBody] GoogleSignInRequestDto request)
        {
            _logger.LogInformation("Google sign-in attempt for app: {App}. IdToken (first 10 chars): {IdTokenStart}",
                                   request.App, request.IdToken?.Substring(0, Math.Min(10, request.IdToken?.Length ?? 0)));

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Google sign-in request failed model validation. Errors: {ValidationErrors}", string.Join(", ", errors));
                return BadRequest(new { message = "Validation failed.", errors = ModelState });
            }
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                _logger.LogWarning("Google sign-in attempt with empty IdToken.");
                return BadRequest(new { message = "Google IdToken is required." });
            }

            try
            {
                var response = await _googleSignInService.SignInAsync(request);
                if (response == null)
                {
                    _logger.LogWarning("Google sign-in failed for app: {App}. SignInAsync returned null (e.g., user not found/approved, invalid token).", request.App);
                    return Unauthorized(new { message = "Google sign-in failed or user not approved." });
                }
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Google sign-in failed due to invalid argument (e.g., bad token). App: {App}", request.App);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Google sign-in failed for app: {App} due to invalid operation (e.g., account issues).", request.App);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Google sign-in for app: {App}", request.App);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during Google sign-in." });
            }
        }

        // POST api/auth/login/facebook
        [AllowAnonymous]
        [HttpPost("login/facebook")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponseDto>> SignFacebookUser([FromBody] FacebookSignInRequestDto request)
        {
            _logger.LogInformation("Facebook sign-in attempt for app: {App}. AccessToken (first 10 chars): {AccessTokenStart}",
                                   request.App, request.AccessToken?.Substring(0, Math.Min(10, request.AccessToken?.Length ?? 0)));

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Facebook sign-in request failed model validation. Errors: {ValidationErrors}", string.Join(", ", errors));
                return BadRequest(new { message = "Validation failed.", errors = ModelState });
            }
            if (string.IsNullOrWhiteSpace(request.AccessToken))
            {
                _logger.LogWarning("Facebook sign-in attempt with empty AccessToken.");
                return BadRequest(new { message = "Facebook AccessToken is required." });
            }

            try
            {
                var response = await _facebookSignInService.SignInAsync(request.AccessToken, request.App);
                if (response == null)
                {
                    _logger.LogWarning("Facebook sign-in failed for app: {App}. SignInAsync returned null (e.g., user not found/approved, invalid token).", request.App);
                    return Unauthorized(new { message = "Facebook sign-in failed or user not approved." });
                }

                _logger.LogInformation("Facebook sign-in successful for app: {App}. Email from token: {Email}", request.App, response.Email);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Facebook sign-in failed due to invalid argument (e.g., bad token). App: {App}", request.App);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Facebook sign-in failed for app: {App} due to invalid operation (e.g., account issues).", request.App);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Facebook sign-in for app: {App}", request.App);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during Facebook sign-in." });
            }
        }
    }
}