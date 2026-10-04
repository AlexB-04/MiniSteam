using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniSteam.Helpers;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtTokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtTokenService tokenService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterDto model,
            CancellationToken cancellationToken)
        {
            var email = model.Email.Trim();
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                return this.ApiProblem(
                    StatusCodes.Status409Conflict,
                    "Account already exists.",
                    "An account with that email already exists.",
                    "AccountExists");
            }

            var user = new User
            {
                Email = email,
                UserName = email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return this.ApiProblem(
                    StatusCodes.Status400BadRequest,
                    "Registration failed.",
                    string.Join(" ", result.Errors.Select(error => error.Description)),
                    "IdentityValidation");
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                _logger.LogError(
                    "Unable to assign the User role during API registration for {UserId}",
                    user.Id);

                return this.ApiProblem(
                    StatusCodes.Status500InternalServerError,
                    "Registration could not be completed.",
                    "The account could not be initialized.",
                    "RoleAssignmentFailed");
            }

            var tokenResponse = await _tokenService.CreateTokenPairAsync(
                user,
                GetClientIp(),
                cancellationToken);

            _logger.LogInformation(
                "API account registered for user {UserId}",
                user.Id);

            return StatusCode(StatusCodes.Status201Created, tokenResponse);
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(
            [FromBody] LoginDto model,
            CancellationToken cancellationToken)
        {
            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning(
                    "API login failed for unknown email from {IpAddress}",
                    GetClientIp());

                return this.ApiProblem(
                    StatusCodes.Status401Unauthorized,
                    "Authentication failed.",
                    "Invalid email or password.",
                    "InvalidCredentials");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                model.Password,
                lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "API login failed for user {UserId}. LockedOut: {LockedOut}",
                    user.Id,
                    result.IsLockedOut);

                return this.ApiProblem(
                    StatusCodes.Status401Unauthorized,
                    "Authentication failed.",
                    "Invalid email or password.",
                    result.IsLockedOut ? "AccountLocked" : "InvalidCredentials");
            }

            var tokenResponse = await _tokenService.CreateTokenPairAsync(
                user,
                GetClientIp(),
                cancellationToken);

            _logger.LogInformation(
                "API login succeeded for user {UserId}",
                user.Id);

            return Ok(tokenResponse);
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequestDto model,
            CancellationToken cancellationToken)
        {
            var result = await _tokenService.RefreshAsync(
                model.RefreshToken,
                GetClientIp(),
                cancellationToken);

            if (!result.Succeeded || result.Value == null)
            {
                return this.ApiProblem(
                    StatusCodes.Status401Unauthorized,
                    "Session refresh failed.",
                    "The refresh token is invalid or expired.",
                    "InvalidRefreshToken");
            }

            return Ok(result.Value);
        }

        [HttpPost("revoke")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Revoke(
            [FromBody] RevokeTokenRequestDto model,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _tokenService.RevokeAsync(
                model.RefreshToken,
                userId,
                GetClientIp(),
                cancellationToken);

            if (!result.Succeeded)
            {
                return this.FromServiceFailure(result, "Session could not be revoked.");
            }

            return Ok(new
            {
                message = "Refresh token revoked successfully."
            });
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult Me()
        {
            return Ok(new
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                email = User.FindFirstValue(ClaimTypes.Email),
                roles = User.FindAll(ClaimTypes.Role)
                    .Select(claim => claim.Value)
                    .ToList()
            });
        }

        private string? GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
