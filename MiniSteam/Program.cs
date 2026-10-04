using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniSteam.Data;
using MiniSteam.Helpers;
using MiniSteam.Models.Entities;
using MiniSteam.Services;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace MiniSteam
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<DataContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<DataContext>()
                .AddDefaultTokenProviders();

            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            });

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "MiniSteam.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

            var jwtIssuer = builder.Configuration["Jwt:Issuer"];
            var jwtAudience = builder.Configuration["Jwt:Audience"];
            var jwtKey = builder.Configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtIssuer) ||
                string.IsNullOrWhiteSpace(jwtAudience) ||
                string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "JWT issuer, audience and key must be configured.");
            }

            byte[] jwtKeyBytes;

            try
            {
                jwtKeyBytes = Convert.FromBase64String(jwtKey);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "JWT key must be a valid Base64 string.",
                    exception);
            }

            if (jwtKeyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT key must contain at least 32 bytes (256 bits)."
                );
            }

            builder.Services.AddAuthentication()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),

                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });

            // Authentication endpoints are intentionally rate-limited by client IP.
            // This complements Identity's account lockout and also slows down requests
            // that target many different accounts from one source.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds))
                            .ToString(CultureInfo.InvariantCulture);
                    }

                    await context.HttpContext.Response.WriteAsync(
                        "Too many authentication requests. Please try again later.",
                        cancellationToken);
                };

                options.AddPolicy("auth", httpContext =>
                {
                    var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown-client";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            });

            builder.Services.AddScoped<IMailHelper, MailHelper>();

            builder.Services.AddScoped<ILibraryService, LibraryService>();
            builder.Services.AddScoped<IWishlistService, WishlistService>();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IPurchaseService, PurchaseService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();

            builder.Services.AddTransient<SeedDb>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var seedDb = scope.ServiceProvider.GetRequiredService<SeedDb>();
                await seedDb.SeedAsync();
            }

            var cultureInfo = new CultureInfo("en-US");

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Low-risk response headers that improve browser-side hardening without
            // interfering with the external screenshots / YouTube media used by MiniSteam.
            app.Use(async (context, next) =>
            {
                context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
                context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.TryAdd(
                    "Permissions-Policy",
                    "camera=(), microphone=(), geolocation=()");

                await next();
            });

            app.UseStaticFiles();

            app.UseRouting();
            app.UseRateLimiter();

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
