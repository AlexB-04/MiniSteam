using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface IJwtTokenService
    {
        Task<TokenResponseDto> CreateTokenPairAsync(
            User user,
            string? ipAddress,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<TokenResponseDto>> RefreshAsync(
            string refreshToken,
            string? ipAddress,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<bool>> RevokeAsync(
            string refreshToken,
            string userId,
            string? ipAddress,
            CancellationToken cancellationToken = default);
    }
}
