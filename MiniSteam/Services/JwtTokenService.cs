using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniSteam.Data;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace MiniSteam.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly UserManager<User> _userManager;
        private readonly DataContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(
            UserManager<User> userManager,
            DataContext context,
            IConfiguration configuration,
            ILogger<JwtTokenService> logger)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<TokenResponseDto> CreateTokenPairAsync(
            User user,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            var cleanupCutoff = DateTime.UtcNow.AddDays(-7);

            var staleTokens = await _context.RefreshTokens
                .Where(token =>
                    token.UserId == user.Id &&
                    (token.ExpiresAt <= DateTime.UtcNow ||
                     (token.RevokedAt.HasValue && token.RevokedAt.Value <= cleanupCutoff)))
                .ToListAsync(cancellationToken);

            if (staleTokens.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(staleTokens);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = CreateAccessToken(user, roles);
            var refreshTokenValue = CreateRefreshTokenValue();
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenDays());

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(refreshTokenValue),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = refreshTokenExpiresAt,
                SecurityStamp = user.SecurityStamp,
                CreatedByIp = NormalizeIp(ipAddress)
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new TokenResponseDto
            {
                Token = accessToken.Token,
                ExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshTokenValue,
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                Roles = roles.ToList()
            };
        }

        public async Task<ServiceResult<TokenResponseDto>> RefreshAsync(
            string refreshToken,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return ServiceResult<TokenResponseDto>.Fail(
                    ServiceResultStatus.Forbidden,
                    "Refresh token is invalid or expired.");
            }

            var tokenHash = HashToken(refreshToken.Trim());

            var storedToken = await _context.RefreshTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(
                    token => token.TokenHash == tokenHash,
                    cancellationToken);

            if (storedToken == null ||
                storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                return ServiceResult<TokenResponseDto>.Fail(
                    ServiceResultStatus.Forbidden,
                    "Refresh token is invalid or expired.");
            }

            if (storedToken.RevokedAt.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(storedToken.ReplacedByTokenHash))
                {
                    var activeTokens = await _context.RefreshTokens
                        .Where(token =>
                            token.UserId == storedToken.UserId &&
                            token.RevokedAt == null &&
                            token.ExpiresAt > DateTime.UtcNow)
                        .ToListAsync(cancellationToken);

                    foreach (var activeToken in activeTokens)
                    {
                        activeToken.RevokedAt = DateTime.UtcNow;
                        activeToken.RevokedByIp = NormalizeIp(ipAddress);
                    }

                    if (activeTokens.Count > 0)
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    _logger.LogWarning(
                        "Refresh-token reuse detected for user {UserId}. Active refresh sessions were revoked.",
                        storedToken.UserId);
                }

                return ServiceResult<TokenResponseDto>.Fail(
                    ServiceResultStatus.Forbidden,
                    "Refresh token is invalid or expired.");
            }

            if (!string.Equals(
                    storedToken.SecurityStamp,
                    storedToken.User.SecurityStamp,
                    StringComparison.Ordinal))
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                storedToken.RevokedByIp = NormalizeIp(ipAddress);
                await _context.SaveChangesAsync(cancellationToken);

                return ServiceResult<TokenResponseDto>.Fail(
                    ServiceResultStatus.Forbidden,
                    "Refresh token is invalid or expired.");
            }

            if (await _userManager.IsLockedOutAsync(storedToken.User))
            {
                return ServiceResult<TokenResponseDto>.Fail(
                    ServiceResultStatus.Forbidden,
                    "Refresh token is invalid or expired.");
            }

            var roles = await _userManager.GetRolesAsync(storedToken.User);
            var accessToken = CreateAccessToken(storedToken.User, roles);
            var replacementValue = CreateRefreshTokenValue();
            var replacementHash = HashToken(replacementValue);
            var replacementExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenDays());

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = NormalizeIp(ipAddress);
            storedToken.ReplacedByTokenHash = replacementHash;

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = storedToken.UserId,
                TokenHash = replacementHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = replacementExpiresAt,
                SecurityStamp = storedToken.User.SecurityStamp,
                CreatedByIp = NormalizeIp(ipAddress)
            });

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Refresh token rotated for user {UserId}",
                storedToken.UserId);

            return ServiceResult<TokenResponseDto>.Success(new TokenResponseDto
            {
                Token = accessToken.Token,
                ExpiresAt = accessToken.ExpiresAt,
                RefreshToken = replacementValue,
                RefreshTokenExpiresAt = replacementExpiresAt,
                Roles = roles.ToList()
            });
        }

        public async Task<ServiceResult<bool>> RevokeAsync(
            string refreshToken,
            string userId,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return ServiceResult<bool>.Fail(
                    ServiceResultStatus.NotFound,
                    "Refresh token was not found.");
            }

            var tokenHash = HashToken(refreshToken.Trim());

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(
                    token => token.TokenHash == tokenHash && token.UserId == userId,
                    cancellationToken);

            if (storedToken == null)
            {
                return ServiceResult<bool>.Fail(
                    ServiceResultStatus.NotFound,
                    "Refresh token was not found.");
            }

            if (!storedToken.RevokedAt.HasValue)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                storedToken.RevokedByIp = NormalizeIp(ipAddress);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Refresh token revoked for user {UserId}",
                userId);

            return ServiceResult<bool>.Success(true);
        }

        private (string Token, DateTime ExpiresAt) CreateAccessToken(
            User user,
            IEnumerable<string> roles)
        {
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key is not configured.");

            var key = new SymmetricSecurityKey(Convert.FromBase64String(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes());

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (
                new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt);
        }

        private int GetAccessTokenMinutes()
        {
            var configured = _configuration.GetValue<int?>("Jwt:AccessTokenMinutes");
            return configured is > 0 and <= 1440 ? configured.Value : 60;
        }

        private int GetRefreshTokenDays()
        {
            var configured = _configuration.GetValue<int?>("Jwt:RefreshTokenDays");
            return configured is > 0 and <= 365 ? configured.Value : 30;
        }

        private static string CreateRefreshTokenValue()
        {
            return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        private static string? NormalizeIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            return ipAddress.Length <= 64
                ? ipAddress
                : ipAddress[..64];
        }
    }
}
