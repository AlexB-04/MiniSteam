using Microsoft.AspNetCore.Identity;

namespace MiniSteam.Data
{
    public class SeedDb
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public SeedDb(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task SeedAsync()
        {
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Security note:
            // Do not automatically promote a hard-coded email address to Admin here.
            // Existing Admin users keep their role in the database. New Admin accounts
            // should be assigned deliberately rather than by matching a public email value.
        }
    }
}
