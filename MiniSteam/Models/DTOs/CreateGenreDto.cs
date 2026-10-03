using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class CreateGenreDto
    {
        [Required(ErrorMessage = "Genre name is required.")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}