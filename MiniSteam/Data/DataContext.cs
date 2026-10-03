using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Models.Entities;

namespace MiniSteam.Data
{
    // DataContext класс, который представляет контекст базы данных для приложения MiniSteam.DataContext наследуется от IdentityDbContext<User>,
    // поэтому содержит как таблицы MiniSteam, так и таблицы ASP.NET Identity.
    public class DataContext : IdentityDbContext<User>
    {
        // DbSet<Game> Games - это свойство, которое представляет коллекцию всех игр в базе данных. Оно позволяет выполнять операции CRUD (создание, чтение, обновление, удаление) с сущностями Game.
        public DbSet<Game> Games { get; set; }

        // DbSet<Genre> Genres - это свойство, которое представляет коллекцию всех жанров в базе данных. Оно позволяет выполнять операции CRUD с сущностями Genre.
        public DbSet<Genre> Genres { get; set; }

        public DbSet<LibraryGame> LibraryGames { get; set; }

        public DbSet<Purchase> Purchases { get; set; }

        public DbSet<PurchaseItem> PurchaseItems { get; set; }

        public DbSet<WishlistItem> WishlistItems { get; set; }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<CartItem> CartItems { get; set; }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<GameScreenshot> GameScreenshots { get; set; }

        // Конструктор класса DataContext, который принимает параметры конфигурации DbContextOptions и передает их базовому классу DbContext.
        // Это позволяет настроить контекст базы данных, например, указать строку подключения к базе данных.
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LibraryGame>()
                .HasIndex(libraryGame => new { libraryGame.UserId, libraryGame.GameId })
                .IsUnique();

            modelBuilder.Entity<Purchase>()
                .Property(purchase => purchase.TotalPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PurchaseItem>()
                .Property(purchaseItem => purchaseItem.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PurchaseItem>()
                .HasOne(purchaseItem => purchaseItem.Game)
                .WithMany()
                .HasForeignKey(purchaseItem  => purchaseItem.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WishlistItem>()
                .HasIndex(wishlistItem => new { wishlistItem.UserId, wishlistItem.GameId })
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasIndex(review => new { review.UserId, review.GameId })
                .IsUnique();

            modelBuilder.Entity<CartItem>()
                .HasIndex(cartItem => new { cartItem.UserId, cartItem.GameId })
                .IsUnique();

            modelBuilder.Entity<Tag>()
                .HasIndex(tag => tag.Name)
                .IsUnique();

            modelBuilder.Entity<Game>()
                .HasMany(game => game.Tags)
                .WithMany(tag => tag.Games)
                .UsingEntity<Dictionary<string, object>>(
                    "GameTags",
                    right => right
                        .HasOne<Tag>()
                        .WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left
                        .HasOne<Game>()
                        .WithMany()
                        .HasForeignKey("GameId")
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.HasKey("GameId", "TagId");
                        join.ToTable("GameTags");
                    });

            modelBuilder.Entity<GameScreenshot>()
                .HasOne(screenshot => screenshot.Game)
                .WithMany(game => game.Screenshots)
                .HasForeignKey(screenshot => screenshot.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameScreenshot>()
                .HasIndex(screenshot => new { screenshot.GameId, screenshot.SortOrder });

        }
    }
}