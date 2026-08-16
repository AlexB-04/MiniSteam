using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Controllers
{
    public class GamesController : Controller
    {
        // Контроллер GamesController, который управляет действиями, связанными с играми в приложении MiniSteam.
        // Он наследуется от базового класса Controller, предоставляемого ASP.NET Core MVC.
        private readonly DataContext _context;

        // Конструктор класса GamesController, который принимает экземпляр DataContext и сохраняет его в приватное поле _context.
        // Это позволяет контроллеру взаимодействовать с базой данных через контекст.
        public GamesController(DataContext context) 
        {
            _context = context;
        }


        // GET: Games
        // Метод Index возвращает представление со списком всех игр, упорядоченных по имени. Он использует контекст базы данных для получения данных о играх и их жанрах.
        public IActionResult Index()
        {
            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name");

            return View(_context.Games.Include(game => game.Genre).OrderBy(game => game.Name));
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
        public async Task<IActionResult> Details(int? id)
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

            return View(game);
        }

        // POST: Games/Create
        // Метод Create обрабатывает POST-запрос для создания новой игры. Он проверяет, является ли модель допустимой, добавляет игру в контекст базы данных и сохраняет изменения.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Game game)
        {
            if (ModelState.IsValid)
            {
                _context.Games.Add(game);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", game.GenreId);

            return View(game);
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

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", game.GenreId);

            return View(game);
        }

        // POST: Games/Edit/5
        // Метод Edit обрабатывает POST-запрос для обновления существующей игры.
        // Он проверяет, является ли модель допустимой, обновляет игру в контексте базы данных и сохраняет изменения.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Game game)
        {
            if (ModelState.IsValid)
            {
                if (!await _context.Games.AnyAsync(g => g.Id == game.Id))
                {
                    return NotFound();
                }

                _context.Games.Update(game);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.GenreId = new SelectList(_context.Genres.OrderBy(genre => genre.Name), "Id", "Name", game.GenreId);

            return View(game);
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

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}