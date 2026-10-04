using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class RevokeTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
