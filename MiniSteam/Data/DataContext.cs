
using Microsoft.EntityFrameworkCore;
using MiniSteam.Models.Entities;

namespace MiniSteam.Data
{
    // DataContext класс, который представляет контекст базы данных для приложения MiniSteam. Он наследуется от DbContext, предоставляемого Entity Framework Core, и используется для взаимодействия с базой данных.
    public class DataContext : DbContext
    {
        // DbSet<Game> Games - это свойство, которое представляет коллекцию всех игр в базе данных. Оно позволяет выполнять операции CRUD (создание, чтение, обновление, удаление) с сущностями Game.
        public DbSet<Game> Games { get; set; }

        // Конструктор класса DataContext, который принимает параметры конфигурации DbContextOptions и передает их базовому классу DbContext. Это позволяет настроить контекст базы данных, например, указать строку подключения к базе данных.
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
    }
}
