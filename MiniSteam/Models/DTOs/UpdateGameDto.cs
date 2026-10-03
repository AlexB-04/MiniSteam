namespace MiniSteam.Models.DTOs
{
    public class UpdateGameDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Developer { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public int? GenreId { get; set; }
        public bool IsPublic { get; set; }
    }
}