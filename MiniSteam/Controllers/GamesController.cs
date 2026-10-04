using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;

namespace MiniSteam.Controllers
{
    public class GamesController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<User> _userManager;

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
            UserManager<User> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
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
                && file.Length <= MaxImageSize;
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
            if (url.StartsWith('/'))
            {
                return true;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsValidTrailerUrl(string url)
        {
            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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
                .OrderBy(game => game.Name)
                .ToListAsync();

            return View(games);
        }

        public async Task<IActionResult> Store(
            string? searchString,
            int? genreId,
            string? tag,
            string? developer,
            string? publisher)
        {
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

            ViewBag.ActiveTag = tag?.Trim();
            ViewBag.ActiveDeveloper = developer?.Trim();
            ViewBag.ActivePublisher = publisher?.Trim();

            LoadGenreList(genreId);

            return View(await games
                .OrderBy(game => game.Name)
                .ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Search(string searchString, int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
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
                ReleaseDate = DateTime.Today
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
                    ReleaseDate = model.ReleaseDate,
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
                ReleaseDate = game.ReleaseDate,
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
                game.ReleaseDate = model.ReleaseDate;
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
            var game = await _context.Games.FindAsync(id);

            if (game == null)
            {
                return NotFound();
            }

            var imageUrl = game.ImageUrl;
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
            return RedirectToAction(nameof(Index));
        }
    }
}
