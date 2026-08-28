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

        public GamesController(
            DataContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<User> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        // Проверяет тип, расширение и размер загруженной картинки.
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

        // GET: Games
        // Административный список всех игр.
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name");

            var games = await _context.Games
                .Include(game => game.Genre)
                .OrderBy(game => game.Name)
                .ToListAsync();

            return View(games);
        }

        // GET: Games/Store
        // Публичный магазин. Показываются только опубликованные игры.
        public IActionResult Store(string? searchString, int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Where(game => game.IsPublic);

            if (!string.IsNullOrEmpty(searchString))
            {
                games = games.Where(game =>
                    game.Name.Contains(searchString) ||
                    (game.Genre != null &&
                     game.Genre.Name.Contains(searchString)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game =>
                    game.GenreId == genreId.Value);
            }

            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                genreId);

            return View(
                games
                    .OrderBy(game => game.Name)
                    .ToList());
        }

        // GET: Games/Search
        // Поиск в административном списке игр.
        [Authorize(Roles = "Admin")]
        public IActionResult Search(string searchString, int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                games = games.Where(game =>
                    game.Name.Contains(searchString) ||
                    (game.Genre != null &&
                     game.Genre.Name.Contains(searchString)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game =>
                    game.GenreId == genreId.Value);
            }

            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                genreId);

            return View(
                "Index",
                games
                    .OrderBy(game => game.Name)
                    .ToList());
        }

        // GET: Games/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name");

            return View();
        }

        // GET: Games/Details/5
        public async Task<IActionResult> Details(
            int? id,
            string? from)
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

            User? currentUser = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

            // Обычный пользователь не может открыть скрытую игру,
            // если только он уже не владеет ею.
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

        // POST: Games/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            GameViewModel model)
        {
            if (model.ImageFile != null &&
                !IsValidImage(model.ImageFile))
            {
                ModelState.AddModelError(
                    nameof(model.ImageFile),
                    "Image must be JPG, JPEG, PNG or WEBP and no larger than 5 MB.");
            }

            if (ModelState.IsValid)
            {
                string? imageUrl = null;

                if (model.ImageFile != null &&
                    model.ImageFile.Length > 0)
                {
                    var extension = Path
                        .GetExtension(model.ImageFile.FileName)
                        .ToLowerInvariant();

                    var fileName =
                        $"{Guid.NewGuid()}{extension}";

                    var folderPath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        "images",
                        "games");

                    Directory.CreateDirectory(folderPath);

                    var filePath = Path.Combine(
                        folderPath,
                        fileName);

                    using (var stream =
                           new FileStream(
                               filePath,
                               FileMode.Create))
                    {
                        await model.ImageFile
                            .CopyToAsync(stream);
                    }

                    imageUrl =
                        $"/images/games/{fileName}";
                }

                var game = new Game
                {
                    Name = model.Name,
                    Description = model.Description,
                    Price = model.Price,
                    ReleaseDate = model.ReleaseDate,
                    Developer = model.Developer,
                    Publisher = model.Publisher,
                    IsPublic = model.IsPublic,
                    GenreId = model.GenreId,
                    ImageUrl = imageUrl
                };

                _context.Games.Add(game);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                model.GenreId);

            return View(model);
        }

        // GET: Games/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games.FindAsync(id);

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
                ReleaseDate = game.ReleaseDate,
                Developer = game.Developer,
                Publisher = game.Publisher,
                IsPublic = game.IsPublic,
                GenreId = game.GenreId,
                ExistingImageUrl = game.ImageUrl
            };

            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                game.GenreId);

            return View(model);
        }

        // POST: Games/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(
    int id,
    GameViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.ImageFile != null &&
                !IsValidImage(model.ImageFile))
            {
                ModelState.AddModelError(
                    nameof(model.ImageFile),
                    "Image must be JPG, JPEG, PNG or WEBP and no larger than 5 MB.");
            }

            if (ModelState.IsValid)
            {
                var game = await _context.Games.FindAsync(id);

                if (game == null)
                {
                    return NotFound();
                }

                game.Name = model.Name;
                game.Description = model.Description;
                game.Price = model.Price;
                game.ReleaseDate = model.ReleaseDate;
                game.Developer = model.Developer;
                game.Publisher = model.Publisher;
                game.IsPublic = model.IsPublic;
                game.GenreId = model.GenreId;

                string? oldImageUrlToDelete = null;
                string? newImageFilePath = null;

                if (model.ImageFile != null &&
                    model.ImageFile.Length > 0)
                {
                    oldImageUrlToDelete = game.ImageUrl;

                    var extension = Path
                        .GetExtension(model.ImageFile.FileName)
                        .ToLowerInvariant();

                    var fileName =
                        $"{Guid.NewGuid()}{extension}";

                    var folderPath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        "images",
                        "games");

                    Directory.CreateDirectory(folderPath);

                    newImageFilePath = Path.Combine(
                        folderPath,
                        fileName);

                    using (var stream = new FileStream(
                        newImageFilePath,
                        FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    game.ImageUrl =
                        $"/images/games/{fileName}";
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Если БД не сохранилась, удаляем только что загруженную
                    // новую картинку, чтобы не оставлять лишний файл на диске.
                    if (!string.IsNullOrEmpty(newImageFilePath) &&
                        System.IO.File.Exists(newImageFilePath))
                    {
                        System.IO.File.Delete(newImageFilePath);
                    }

                    throw;
                }

                // Старую картинку удаляем только после успешного сохранения БД.
                if (!string.IsNullOrEmpty(oldImageUrlToDelete) &&
                    oldImageUrlToDelete.StartsWith("/images/games/"))
                {
                    var oldFileName =
                        Path.GetFileName(oldImageUrlToDelete);

                    var oldFilePath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        "images",
                        "games",
                        oldFileName);

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.GenreId = new SelectList(
                _context.Genres.OrderBy(genre => genre.Name),
                "Id",
                "Name",
                model.GenreId);

            return View(model);
        }

        // GET: Games/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games.FindAsync(id);

            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        // POST: Games/Delete/5
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

            // Запоминаем путь картинки до удаления игры.
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

            // Удаляем локальный файл картинки
            // только после успешного удаления игры из БД.
            if (!string.IsNullOrEmpty(imageUrl) &&
                imageUrl.StartsWith("/images/games/"))
            {
                var fileName =
                    Path.GetFileName(imageUrl);

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

            return RedirectToAction(nameof(Index));
        }
    }
}