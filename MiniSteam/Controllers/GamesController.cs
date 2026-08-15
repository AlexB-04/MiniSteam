using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        // Метод Index возвращает представление со списком всех игр, отсортированных по имени. Он использует LINQ для сортировки игр и передает их в представление.
        public IActionResult Index()
        {
            return View(_context.Games.OrderBy(g => g.Name));
        }

        // GET Games/Create
        // Метод Create возвращает представление для создания новой игры. Он используется для отображения формы, где пользователь может ввести данные новой игры.
        public IActionResult Create()
        {
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
            var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
            if (game == null)
            {
                return NotFound();
            }
            return View(game);
        }

        // POST: Games/Create
        // Метод Create обрабатывает POST-запрос для создания новой игры. Он проверяет, является ли модель допустимой, добавляет новую игру в контекст базы данных и сохраняет изменения.
        // Если модель недействительна, возвращается представление с ошибками.
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

            return View(game);
        }

        // POST: Games/Edit/5
        // Метод Edit обрабатывает POST-запрос для обновления существующей игры.
        // Он проверяет, является ли модель допустимой, и если игра с указанным ID существует, обновляет ее в контексте базы данных и сохраняет изменения.
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