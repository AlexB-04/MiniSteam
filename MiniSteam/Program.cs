using Microsoft.AspNetCore.Identity;
using MiniSteam.Models.Entities;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
namespace MiniSteam
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Создаём контекст базы данных и настраиваем его для использования SQL Server с использованием строки подключения из конфигурации.
            builder.Services.AddDbContext<DataContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<User, IdentityRole>().AddEntityFrameworkStores<DataContext>().AddDefaultTokenProviders();

            builder.Services.AddTransient<SeedDb>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var seedDb = scope.ServiceProvider.GetRequiredService<SeedDb>();
                await seedDb.SeedAsync();
            }

            // Валюта США и форматирование чисел и дат в соответствии с американскими стандартами.
            var cultureInfo = new CultureInfo("en-US");

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Games}/{action=Store}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}