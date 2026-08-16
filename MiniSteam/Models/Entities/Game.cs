using System.ComponentModel.DataAnnotations.Schema;

namespace MiniSteam.Models.Entities
{
    public class Game
    {
        // Каждый ID индивидуален и уникален для каждой игры в базе данных.
        public int Id { get; set; }

        // Название игры, которое будет отображаться пользователям.
        public string? Name { get; set; }

        // Краткое описание игры, которое поможет пользователям понять, о чем игра.
        public string? Description { get; set; }

        // Цена игры в Долларах. Должна быть положительным числом.
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Дата выпуска игры. Должна быть в формате "yyyy-MM-dd".
        public DateTime ReleaseDate { get; set; }

        // Разработчик игры. Это может быть студия или индивидуальный разработчик.
        public string? Developer { get; set; }

        // Издатель игры. Это компания, которая распространяет игру.
        public string? Publisher { get; set; }

        // URL изображения игры. Это может быть обложка или скриншот игры.
        public string? ImageUrl { get; set; }

        // Флаг, указывающий, является ли игра публичной или нет. Если игра публичная, она доступна для всех пользователей.
        public bool IsPublic { get; set; }

        // Внешний ключ для жанра игры. Это связывает игру с определенным жанром в базе данных.
        public int? GenreId { get; set; }

        // Навигационное свойство для жанра игры. Это позволяет легко получать информацию о жанре, к которому принадлежит игра.
        public Genre? Genre { get; set; }
    }
}