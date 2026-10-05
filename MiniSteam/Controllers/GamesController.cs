using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    public class GamesController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<User> _userManager;
        private readonly IGameBuildStorageService _gameBuildStorage;
        private readonly ILogger<GamesController> _logger;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly string[] AllowedImageContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private const long MaxImageSize = 5 * 1024 * 1024;
        private const int MaxTags = 20;
        private const int MaxScreenshots = 12;

        public GamesController(
            DataContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<User> userManager,
            IGameBuildStorageService gameBuildStorage,
            ILogger<GamesController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _gameBuildStorage = gameBuildStorage;
            _logger = logger;
        }

        private bool IsValidImage(IFormFile file)
        {
            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            var contentType = file.ContentType
                .ToLowerInvariant();

            return AllowedImageExtensions.Contains(extension)
                && AllowedImageContentTypes.Contains(contentType)
                && file.Length > 0
                && file.Length <= MaxImageSize
                && HasValidImageSignature(file, extension);
        }

        private static bool HasValidImageSignature(IFormFile file, string extension)
        {
            Span<byte> header = stackalloc byte[12];

            using var stream = file.OpenReadStream();
            var bytesRead = stream.Read(header);

            return extension switch
            {
                ".jpg" or ".jpeg" =>
                    bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF,

                ".png" =>
                    bytesRead >= 8 &&
                    header[..8].SequenceEqual(new byte[]
                    {
                        0x89, 0x50, 0x4E, 0x47,
                        0x0D, 0x0A, 0x1A, 0x0A
                    }),

                ".webp" =>
                    bytesRead >= 12 &&
                    header[..4].SequenceEqual("RIFF"u8) &&
                    header[8..12].SequenceEqual("WEBP"u8),

                _ => false
            };
        }

        private void LoadGenreList(int? selectedGenreId = null)
        {
            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                selectedGenreId);
        }

        private void ValidateStoreContent(GameViewModel model)
        {
            var tagNames = ParseTags(model.TagsText);
            var screenshotUrls = ParseScreenshotUrls(model.ScreenshotUrlsText);

            if (tagNames.Count > MaxTags)
            {
                ModelState.AddModelError(
                    nameof(model.TagsText),
                    $"A game can have up to {MaxTags} tags.");
            }

            if (tagNames.Any(tag => tag.Length > 50))
            {
                ModelState.AddModelError(
                    nameof(model.TagsText),
                    "Each tag can contain up to 50 characters.");
            }

            if (screenshotUrls.Count > MaxScreenshots)
            {
                ModelState.AddModelError(
                    nameof(model.ScreenshotUrlsText),
                    $"A game can have up to {MaxScreenshots} screenshots.");
            }

            if (screenshotUrls.Any(url => url.Length > 500 || !IsValidScreenshotUrl(url)))
            {
                ModelState.AddModelError(
                    nameof(model.ScreenshotUrlsText),
                    "Each screenshot must be an http/https URL or a local path beginning with '/'.");
            }

            if (!string.IsNullOrWhiteSpace(model.TrailerUrl) && !IsValidTrailerUrl(model.TrailerUrl))
            {
                ModelState.AddModelError(
                    nameof(model.TrailerUrl),
                    "Trailer must be an http/https URL. YouTube watch, youtu.be, embed, MP4 and WEBM URLs are supported.");
            }

            if (!Enum.IsDefined(typeof(GameReleaseStatus), model.ReleaseStatus))
            {
                ModelState.AddModelError(
                    nameof(model.ReleaseStatus),
                    "Select a valid release status.");
            }

            if (model.Price > 0 &&
                model.DiscountPercent > 0 &&
                model.DiscountStartDate.HasValue &&
                model.DiscountEndDate.HasValue &&
                model.DiscountStartDate.Value.Date > model.DiscountEndDate.Value.Date)
            {
                ModelState.AddModelError(
                    nameof(model.DiscountEndDate),
                    "Discount end date must be on or after the start date.");
            }
        }

        private static List<string> ParseTags(string? tagsText)
        {
            if (string.IsNullOrWhiteSpace(tagsText))
            {
                return new List<string>();
            }

            return tagsText
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseScreenshotUrls(string? screenshotUrlsText)
        {
            if (string.IsNullOrWhiteSpace(screenshotUrlsText))
            {
                return new List<string>();
            }

            return screenshotUrlsText
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(url => url.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsValidScreenshotUrl(string url)
        {
            if (IsSafeLocalMediaPath(url))
            {
                return true;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        private static bool IsValidTrailerUrl(string url)
        {
            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        private static bool IsSafeLocalMediaPath(string url)
        {
            return url.StartsWith('/')
                && !url.StartsWith("//", StringComparison.Ordinal)
                && !url.Contains('\\')
                && !url.Any(char.IsControl);
        }

        private async Task ApplyStoreContentAsync(
            Game game,
            IReadOnlyCollection<string> tagNames,
            IReadOnlyCollection<string> screenshotUrls)
        {
            var tagNameList = tagNames.ToList();

            var existingTags = tagNameList.Count == 0
                ? new List<Tag>()
                : await _context.Tags
                    .Where(tag => tagNameList.Contains(tag.Name))
                    .ToListAsync();

            game.Tags.Clear();

            foreach (var tagName in tagNameList)
            {
                var tag = existingTags.FirstOrDefault(existingTag =>
                    string.Equals(existingTag.Name, tagName, StringComparison.OrdinalIgnoreCase));

                if (tag == null)
                {
                    tag = new Tag
                    {
                        Name = tagName
                    };

                    _context.Tags.Add(tag);
                    existingTags.Add(tag);
                }

                game.Tags.Add(tag);
            }

            if (game.Screenshots.Count > 0)
            {
                _context.GameScreenshots.RemoveRange(game.Screenshots.ToList());
                game.Screenshots.Clear();
            }

            var sortOrder = 0;

            foreach (var screenshotUrl in screenshotUrls)
            {
                game.Screenshots.Add(new GameScreenshot
                {
                    Url = screenshotUrl,
                    SortOrder = sortOrder++
                });
            }
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            var fileName = $"{Guid.NewGuid()}{extension}";

            var folderPath = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "images",
                "games");

            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(
                folderPath,
                fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/games/{fileName}";
        }

        private void DeleteLocalGameImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) ||
                !imageUrl.StartsWith("/images/games/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);
            var filePath = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "images",
                "games",
                fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            LoadGenreList();

            var games = await _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Build)
                .OrderBy(game => game.Name)
                .ToListAsync();

            return View(games);
        }

        public async Task<IActionResult> Store(
            string? searchString,
            int? genreId,
            string? tag,
            string? developer,
            string? publisher,
            string? section)
        {
            var today = DateTime.Today;
            var newReleaseCutoff = today.AddDays(-90);

            var games = _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Where(game => game.IsPublic)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();

                games = games.Where(game =>
                    game.Name.Contains(search) ||
                    game.Developer.Contains(search) ||
                    (game.Publisher != null && game.Publisher.Contains(search)) ||
                    (game.Genre != null && game.Genre.Name.Contains(search)) ||
                    game.Tags.Any(gameTag => gameTag.Name.Contains(search)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = tag.Trim();
                games = games.Where(game => game.Tags.Any(gameTag => gameTag.Name == normalizedTag));
            }

            if (!string.IsNullOrWhiteSpace(developer))
            {
                var normalizedDeveloper = developer.Trim();
                games = games.Where(game => game.Developer == normalizedDeveloper);
            }

            if (!string.IsNullOrWhiteSpace(publisher))
            {
                var normalizedPublisher = publisher.Trim();
                games = games.Where(game => game.Publisher == normalizedPublisher);
            }

            var normalizedSection = section?.Trim().ToLowerInvariant();

            if (normalizedSection is not ("featured" or "specials" or "new" or "earlyaccess" or "free" or "comingsoon"))
            {
                normalizedSection = null;
            }

            games = normalizedSection switch
            {
                "featured" => games.Where(game => game.IsFeatured),
                "specials" => games.Where(game =>
                    game.Price > 0 &&
                    game.ReleaseStatus != GameReleaseStatus.ComingSoon &&
                    game.DiscountPercent > 0 &&
                    (!game.DiscountStartDate.HasValue || game.DiscountStartDate.Value <= today) &&
                    (!game.DiscountEndDate.HasValue || game.DiscountEndDate.Value >= today)),
                "new" => games.Where(game =>
                    game.ReleaseStatus == GameReleaseStatus.Released &&
                    game.ReleaseDate >= newReleaseCutoff &&
                    game.ReleaseDate <= today),
                "earlyaccess" => games.Where(game =>
                    game.ReleaseStatus == GameReleaseStatus.EarlyAccess),
                "free" => games.Where(game =>
                    game.Price == 0 &&
                    game.ReleaseStatus != GameReleaseStatus.ComingSoon),
                "comingsoon" => games.Where(game =>
                    game.ReleaseStatus == GameReleaseStatus.ComingSoon),
                _ => games
            };

            ViewBag.ActiveTag = tag?.Trim();
            ViewBag.ActiveDeveloper = developer?.Trim();
            ViewBag.ActivePublisher = publisher?.Trim();

            LoadGenreList(genreId);

            var discoveryQuery = _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Where(game => game.IsPublic);

            var model = new StoreViewModel
            {
                Games = await games
                    .OrderBy(game => game.Name)
                    .ToListAsync(),

                ActiveSection = normalizedSection,

                FeaturedGames = await discoveryQuery
                    .Where(game => game.IsFeatured)
                    .OrderByDescending(game => game.ReleaseDate)
                    .Take(4)
                    .ToListAsync(),

                SpecialOfferGames = await discoveryQuery
                    .Where(game =>
                        game.Price > 0 &&
                        game.DiscountPercent > 0 &&
                        (!game.DiscountStartDate.HasValue || game.DiscountStartDate.Value <= today) &&
                        (!game.DiscountEndDate.HasValue || game.DiscountEndDate.Value >= today))
                    .OrderByDescending(game => game.DiscountPercent)
                    .Take(4)
                    .ToListAsync(),

                NewReleaseGames = await discoveryQuery
                    .Where(game =>
                        game.ReleaseStatus == GameReleaseStatus.Released &&
                        game.ReleaseDate >= newReleaseCutoff &&
                        game.ReleaseDate <= today)
                    .OrderByDescending(game => game.ReleaseDate)
                    .Take(4)
                    .ToListAsync(),

                EarlyAccessGames = await discoveryQuery
                    .Where(game => game.ReleaseStatus == GameReleaseStatus.EarlyAccess)
                    .OrderByDescending(game => game.ReleaseDate)
                    .Take(4)
                    .ToListAsync(),

                FreeGames = await discoveryQuery
                    .Where(game =>
                        game.Price == 0 &&
                        game.ReleaseStatus != GameReleaseStatus.ComingSoon)
                    .OrderBy(game => game.Name)
                    .Take(4)
                    .ToListAsync(),

                ComingSoonGames = await discoveryQuery
                    .Where(game => game.ReleaseStatus == GameReleaseStatus.ComingSoon)
                    .OrderBy(game => game.ReleaseDate)
                    .Take(4)
                    .ToListAsync()
            };

            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Search(string searchString, int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Include(game => game.Build)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();

                games = games.Where(game =>
                    game.Name.Contains(search) ||
                    game.Developer.Contains(search) ||
                    (game.Publisher != null && game.Publisher.Contains(search)) ||
                    (game.Genre != null && game.Genre.Name.Contains(search)) ||
                    game.Tags.Any(tag => tag.Name.Contains(search)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            LoadGenreList(genreId);

            return View(
                "Index",
                await games.OrderBy(game => game.Name).ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            LoadGenreList();

            return View(new GameViewModel
            {
                ReleaseDate = DateTime.Today,
                ReleaseStatus = GameReleaseStatus.Released
            });
        }

        public async Task<IActionResult> Details(int? id, string? from)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Include(game => game.Screenshots)
                .FirstOrDefaultAsync(game => game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            User? currentUser = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

            if (!game.IsPublic && !User.IsInRole("Admin"))
            {
                if (currentUser == null)
                {
                    return NotFound();
                }

                var ownsHiddenGame = await _context.LibraryGames
                    .AnyAsync(libraryGame =>
                        libraryGame.UserId == currentUser.Id &&
                        libraryGame.GameId == game.Id);

                if (!ownsHiddenGame)
                {
                    return NotFound();
                }
            }

            var reviews = await _context.Reviews
                .Include(review => review.User)
                .Include(review => review.Votes)
                .Where(review => review.GameId == game.Id)
                .OrderByDescending(review => review.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.ReviewCount = reviews.Count;
            ViewBag.CanReview = false;
            ViewBag.CurrentUserId = null;
            ViewBag.IsInLibrary = false;
            ViewBag.IsInWishlist = false;
            ViewBag.IsInCart = false;

            if (currentUser != null)
            {
                ViewBag.CurrentUserId = currentUser.Id;

                ViewBag.IsInLibrary = await _context.LibraryGames
                    .AnyAsync(libraryGame =>
                        libraryGame.UserId == currentUser.Id &&
                        libraryGame.GameId == game.Id);

                ViewBag.IsInWishlist = await _context.WishlistItems
                    .AnyAsync(wishlistItem =>
                        wishlistItem.UserId == currentUser.Id &&
                        wishlistItem.GameId == game.Id);

                ViewBag.IsInCart = await _context.CartItems
                    .AnyAsync(cartItem =>
                        cartItem.UserId == currentUser.Id &&
                        cartItem.GameId == game.Id);

                var alreadyReviewed = await _context.Reviews
                    .AnyAsync(review =>
                        review.UserId == currentUser.Id &&
                        review.GameId == game.Id);

                ViewBag.CanReview =
                    ViewBag.IsInLibrary == true &&
                    !alreadyReviewed;
            }

            ViewBag.From = from;

            return View(game);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(GameViewModel model)
        {
            if (model.ImageFile != null && !IsValidImage(model.ImageFile))
            {
                ModelState.AddModelError(
                    nameof(model.ImageFile),
                    "Image must be JPG, JPEG, PNG or WEBP and no larger than 5 MB.");
            }

            ValidateStoreContent(model);

            if (ModelState.IsValid)
            {
                string? imageUrl = null;

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    imageUrl = await SaveImageAsync(model.ImageFile);
                }

                var game = new Game
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim(),
                    Price = model.Price,
                    DiscountPercent = model.Price == 0 ? 0 : model.DiscountPercent,
                    DiscountStartDate = model.Price == 0 || model.DiscountPercent == 0
                        ? null
                        : model.DiscountStartDate?.Date,
                    DiscountEndDate = model.Price == 0 || model.DiscountPercent == 0
                        ? null
                        : model.DiscountEndDate?.Date,
                    ReleaseDate = model.ReleaseDate,
                    ReleaseStatus = model.ReleaseStatus,
                    IsFeatured = model.IsFeatured,
                    Developer = model.Developer.Trim(),
                    Publisher = model.Publisher?.Trim(),
                    IsPublic = model.IsPublic,
                    GenreId = model.GenreId,
                    ImageUrl = imageUrl,
                    TrailerUrl = model.TrailerUrl?.Trim(),
                    MinimumSystemRequirements = model.MinimumSystemRequirements?.Trim(),
                    RecommendedSystemRequirements = model.RecommendedSystemRequirements?.Trim()
                };

                _context.Games.Add(game);

                await ApplyStoreContentAsync(
                    game,
                    ParseTags(model.TagsText),
                    ParseScreenshotUrls(model.ScreenshotUrlsText));

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    DeleteLocalGameImage(imageUrl);
                    throw;
                }

                _logger.LogInformation(
                    "MVC admin created game {GameId}: {GameName}",
                    game.Id,
                    game.Name);

                return RedirectToAction(nameof(Index));
            }

            LoadGenreList(model.GenreId);
            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .Include(game => game.Tags)
                .Include(game => game.Screenshots)
                .FirstOrDefaultAsync(game => game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            var model = new GameViewModel
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description,
                Price = game.Price,
                DiscountPercent = game.DiscountPercent,
                DiscountStartDate = game.DiscountStartDate,
                DiscountEndDate = game.DiscountEndDate,
                ReleaseDate = game.ReleaseDate,
                ReleaseStatus = game.ReleaseStatus,
                IsFeatured = game.IsFeatured,
                Developer = game.Developer,
                Publisher = game.Publisher,
                IsPublic = game.IsPublic,
                GenreId = game.GenreId,
                ExistingImageUrl = game.ImageUrl,
                TrailerUrl = game.TrailerUrl,
                TagsText = string.Join(", ", game.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name)),
                ScreenshotUrlsText = string.Join(
                    Environment.NewLine,
                    game.Screenshots.OrderBy(screenshot => screenshot.SortOrder).Select(screenshot => screenshot.Url)),
                MinimumSystemRequirements = game.MinimumSystemRequirements,
                RecommendedSystemRequirements = game.RecommendedSystemRequirements
            };

            LoadGenreList(game.GenreId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, GameViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.ImageFile != null && !IsValidImage(model.ImageFile))
            {
                ModelState.AddModelError(
                    nameof(model.ImageFile),
                    "Image must be JPG, JPEG, PNG or WEBP and no larger than 5 MB.");
            }

            ValidateStoreContent(model);

            if (ModelState.IsValid)
            {
                var game = await _context.Games
                    .Include(game => game.Tags)
                    .Include(game => game.Screenshots)
                    .FirstOrDefaultAsync(game => game.Id == id);

                if (game == null)
                {
                    return NotFound();
                }

                game.Name = model.Name.Trim();
                game.Description = model.Description?.Trim();
                game.Price = model.Price;
                game.DiscountPercent = model.Price == 0 ? 0 : model.DiscountPercent;
                game.DiscountStartDate = model.Price == 0 || model.DiscountPercent == 0
                    ? null
                    : model.DiscountStartDate?.Date;
                game.DiscountEndDate = model.Price == 0 || model.DiscountPercent == 0
                    ? null
                    : model.DiscountEndDate?.Date;
                game.ReleaseDate = model.ReleaseDate;
                game.ReleaseStatus = model.ReleaseStatus;
                game.IsFeatured = model.IsFeatured;
                game.Developer = model.Developer.Trim();
                game.Publisher = model.Publisher?.Trim();
                game.IsPublic = model.IsPublic;
                game.GenreId = model.GenreId;
                game.TrailerUrl = model.TrailerUrl?.Trim();
                game.MinimumSystemRequirements = model.MinimumSystemRequirements?.Trim();
                game.RecommendedSystemRequirements = model.RecommendedSystemRequirements?.Trim();

                await ApplyStoreContentAsync(
                    game,
                    ParseTags(model.TagsText),
                    ParseScreenshotUrls(model.ScreenshotUrlsText));

                string? oldImageUrlToDelete = null;
                string? newImageUrl = null;

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    oldImageUrlToDelete = game.ImageUrl;
                    newImageUrl = await SaveImageAsync(model.ImageFile);
                    game.ImageUrl = newImageUrl;
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    DeleteLocalGameImage(newImageUrl);
                    throw;
                }

                if (!string.IsNullOrEmpty(oldImageUrlToDelete))
                {
                    DeleteLocalGameImage(oldImageUrlToDelete);
                }

                _logger.LogInformation(
                    "MVC admin updated game {GameId}: {GameName}",
                    game.Id,
                    game.Name);

                return RedirectToAction(nameof(Index));
            }

            LoadGenreList(model.GenreId);
            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .Include(game => game.Genre)
                .FirstOrDefaultAsync(game => game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await _context.Games
                .Include(item => item.Build)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            var imageUrl = game.ImageUrl;
            var buildArchiveFileName = game.Build?.ArchiveFileName;
            _context.Games.Remove(game);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "This game cannot be deleted because it has purchase history.";

                return RedirectToAction(nameof(Index));
            }

            DeleteLocalGameImage(imageUrl);
            _gameBuildStorage.DeleteArchive(buildArchiveFileName);

            _logger.LogInformation(
                "MVC admin deleted game {GameId}: {GameName}",
                game.Id,
                game.Name);

            return RedirectToAction(nameof(Index));
        }
    }
}
