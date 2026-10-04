using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
