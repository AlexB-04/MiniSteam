using MiniSteam.Models.Entities;

namespace MiniSteam.Models.ViewModels
{
    public class StoreViewModel
    {
        public List<Game> Games { get; set; } = new();
        public List<Game> FeaturedGames { get; set; } = new();
        public List<Game> SpecialOfferGames { get; set; } = new();
        public List<Game> NewReleaseGames { get; set; } = new();
        public List<Game> EarlyAccessGames { get; set; } = new();
        public List<Game> FreeGames { get; set; } = new();
        public List<Game> ComingSoonGames { get; set; } = new();

        public string? ActiveSection { get; set; }
    }
}
