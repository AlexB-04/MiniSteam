using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.ViewComponents
{
    public class CartCountViewComponent : ViewComponent
    {
        private readonly ICartService _cartService;
        private readonly UserManager<User> _userManager;

        public CartCountViewComponent(
            ICartService cartService,
            UserManager<User> userManager)
        {
            _cartService = cartService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);

            if (user == null)
            {
                return Content(string.Empty);
            }

            var count = await _cartService.GetCountAsync(user.Id);
            return View(count);
        }
    }
}
