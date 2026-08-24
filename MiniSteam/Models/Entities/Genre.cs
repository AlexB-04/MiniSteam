using System.ComponentModel.DataAnnotations;
namespace MiniSteam.Models.Entities
{
    public class Genre
    {
        // Свойство Id представляет уникальный идентификатор жанра. Оно используется для идентификации жанра в базе данных и в приложении.
        public int Id { get; set; }

        // Свойство Name представляет название жанра. Оно используется для отображения и идентификации жанра в приложении.
        [Required(ErrorMessage = "Genre name is required.")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Свойство Games представляет коллекцию игр, связанных с этим жанром. Оно используется для навигации между жанрами и играми в приложении.
        public ICollection<Game> Games { get; set; } = new List<Game>();
    }
}