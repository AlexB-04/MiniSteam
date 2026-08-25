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
        // Контроллер GamesController, который управляет действиями, связанными с играми в приложении MiniSteam.
        // Он наследуется от базового класса Controller, предоставляемого ASP.NET Core MVC.
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // Конструктор класса GamesController, который принимает экземпляр DataContext и сохраняет его в приватное поле _context.
        // Это позволяет контроллеру взаимодействовать с базой данных через контекст.
        public GamesController(DataContext context, IWebHostEnvironment webHostEnvironment) 
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }


        // GET: Games
        // Метод Index возвращает представление со списком всех игр, упорядоченных по имени. Он использует контекст базы данных для получения данных о играх и их жанрах.
        public IActionResult Index()
        {
            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name");

            return View(_context.Games.Include(game => game.Genre).OrderBy(game => game.Name));
        }

        // GET: Games/Store
        public IActionResult Store(string? searchString, int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Where(game => game.IsPublic);

            if (!string.IsNullOrEmpty(searchString))
            {
                games = games.Where(game => game.Name.Contains(searchString) || (game.Genre != null && game.Genre.Name.Contains(searchString)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", genreId);

            return View(games.OrderBy(game => game.Name).ToList());
        }

        // GET: Games/Search
        // Метод Search возвращает представление со списком игр, которые соответствуют заданной строке поиска.
        // Он фильтрует игры по имени и упорядочивает их по имени.
        public IActionResult Search(string searchString, int? genreId)
        {
            var games = _context.Games.Include(game => game.Genre).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                games = games.Where(game => game.Name.Contains(searchString) || (game.Genre != null && game.Genre.Name.Contains(searchString)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", genreId);

            return View("Index", games.OrderBy(game => game.Name).ToList());
        }

        // GET Games/Create
        // Метод Create возвращает представление для создания новой игры. Он используется для отображения формы, где пользователь может ввести данные новой игры.
        public IActionResult Create()
        {
            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name");

            return View();
        }

        // GET: Games/Details/5
        // Метод Details возвращает представление с подробной информацией о конкретной игре, идентифицируемой по ее ID.
        // Если ID не указан или игра с таким ID не найдена, возвращается ошибка NotFound.
        public async Task<IActionResult> Details(int? id, string? from)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games.Include(game => game.Genre).FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            ViewBag.From = from;
            return View(game);
        }

        // POST: Games/Create
        // Метод Create обрабатывает POST-запрос для создания новой игры. Он проверяет, является ли модель допустимой, добавляет игру в контекст базы данных и сохраняет изменения.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GameViewModel model)
        {
            if (ModelState.IsValid)
            {
                string? imageUrl = null;

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var extension = Path.GetExtension(model.ImageFile.FileName);
                    var fileName = $"{Guid.NewGuid()}{extension}";

                    var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "games");

                    var filePath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    imageUrl = $"/images/games/{fileName}";
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

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", model.GenreId);

            return View(model);
        }

        // Edit: Games/Edit/5
        // Метод Edit возвращает представление для редактирования существующей игры, идентифицируемой по ее ID. Если ID не указан или игра с таким ID не найдена, возвращается ошибка NotFound.
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

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", game.GenreId);

            return View(model);
        }

        // POST: Games/Edit/5
        // Метод Edit обрабатывает POST-запрос для обновления существующей игры.
        // Он проверяет, является ли модель допустимой, обновляет игру в контексте базы данных и сохраняет изменения.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GameViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
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

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    // Запоминаем старую картинку ДО изменения ImageUrl
                    var oldImageUrl = game.ImageUrl;

                    var extension = Path.GetExtension(model.ImageFile.FileName);
                    var fileName = $"{Guid.NewGuid()}{extension}";

                    var folderPath = Path.Combine(_webHostEnvironment.WebRootPath,"images","games");

                    var filePath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    // Новая картинка успешно сохранилась.
                    // Теперь можно удалить старую.
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        var oldFileName = Path.GetFileName(oldImageUrl);

                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath,"images","games",oldFileName);

                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    game.ImageUrl = $"/images/games/{fileName}";
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", model.GenreId);

            return View(model);
        }

        // GET: Games/Delete/5
        // Метод Delete возвращает представление для подтверждения удаления игры, идентифицируемой по ее ID.
        // Если ID не указан или игра с таким ID не найдена, возвращается ошибка NotFound.
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
        // Метод DeleteConfirmed обрабатывает POST-запрос для удаления игры, идентифицируемой по ее ID. Он ищет игру в контексте базы данных, удаляет ее и сохраняет изменения.
        // Если игра не найдена, возвращается ошибка NotFound.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await _context.Games.FindAsync(id);

            if (game == null)
            {
                return NotFound();
            }

            // Запоминаем путь картинки до удаления игры
            var imageUrl = game.ImageUrl;

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            // Если у игры была загруженная локальная картинка — удаляем файл
            if (!string.IsNullOrEmpty(imageUrl) &&
                imageUrl.StartsWith("/images/games/"))
            {
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

            return RedirectToAction(nameof(Index));
        }
    }
}