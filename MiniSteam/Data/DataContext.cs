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

        public DbSet<ReviewVote> ReviewVotes { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<GameBuild> GameBuilds { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<PaymentItem> PaymentItems { get; set; }

        public DbSet<PaymentEvent> PaymentEvents { get; set; }

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

            modelBuilder.Entity<ReviewVote>()
                .HasIndex(vote => new { vote.ReviewId, vote.UserId })
                .IsUnique();

            modelBuilder.Entity<ReviewVote>()
                .HasOne(vote => vote.Review)
                .WithMany(review => review.Votes)
                .HasForeignKey(vote => vote.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewVote>()
                .HasOne(vote => vote.User)
                .WithMany()
                .HasForeignKey(vote => vote.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(token => token.TokenHash)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(token => token.UserId);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameBuild>()
                .HasIndex(build => build.GameId)
                .IsUnique();

            modelBuilder.Entity<GameBuild>()
                .HasOne(build => build.Game)
                .WithOne(game => game.Build)
                .HasForeignKey<GameBuild>(build => build.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Provider)
                .HasMaxLength(100);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.ProviderReference)
                .HasMaxLength(160);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Currency)
                .HasMaxLength(8);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Subtotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.TaxRate)
                .HasPrecision(8, 6);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.TaxAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.RefundReference)
                .HasMaxLength(160);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.RefundReason)
                .HasMaxLength(500);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.RefundAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.ProviderReference)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => new { payment.UserId, payment.CreatedAt });

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.PurchaseId)
                .IsUnique()
                .HasFilter("[PurchaseId] IS NOT NULL");

            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.User)
                .WithMany()
                .HasForeignKey(payment => payment.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.Purchase)
                .WithOne(purchase => purchase.Payment)
                .HasForeignKey<Payment>(payment => payment.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaymentItem>()
                .Property(item => item.GameName)
                .HasMaxLength(200);

            modelBuilder.Entity<PaymentItem>()
                .Property(item => item.OriginalPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PaymentItem>()
                .Property(item => item.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PaymentItem>()
                .HasOne(item => item.Payment)
                .WithMany(payment => payment.Items)
                .HasForeignKey(item => item.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentEvent>()
                .Property(paymentEvent => paymentEvent.ProviderEventId)
                .HasMaxLength(160);

            modelBuilder.Entity<PaymentEvent>()
                .Property(paymentEvent => paymentEvent.EventType)
                .HasMaxLength(32);

            modelBuilder.Entity<PaymentEvent>()
                .HasIndex(paymentEvent => paymentEvent.ProviderEventId)
                .IsUnique();

            modelBuilder.Entity<PaymentEvent>()
                .HasIndex(paymentEvent => new { paymentEvent.PaymentId, paymentEvent.ReceivedAt });

            modelBuilder.Entity<PaymentEvent>()
                .HasOne(paymentEvent => paymentEvent.Payment)
                .WithMany(payment => payment.Events)
                .HasForeignKey(paymentEvent => paymentEvent.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);


        }
    }
}