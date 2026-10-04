using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniSteam.Models.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ValidateNever]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        [StringLength(64)]
        public string? ReplacedByTokenHash { get; set; }

        [StringLength(100)]
        public string? SecurityStamp { get; set; }

        [StringLength(64)]
        public string? CreatedByIp { get; set; }

        [StringLength(64)]
        public string? RevokedByIp { get; set; }

        [NotMapped]
        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
