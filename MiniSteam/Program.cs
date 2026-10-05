using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniSteam.Data;
using MiniSteam.Helpers;
using MiniSteam.Middleware;
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

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = GameBuildStorageService.DefaultMaxArchiveSizeBytes;
            });

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
                    "JWT issuer, audience and key must be configured. " +
                    "Use User Secrets or environment variables for the key.");
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

            var accessTokenMinutes = builder.Configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 60;
            var refreshTokenDays = builder.Configuration.GetValue<int?>("Jwt:RefreshTokenDays") ?? 30;

            if (accessTokenMinutes <= 0 || accessTokenMinutes > 1440)
            {
                throw new InvalidOperationException(
                    "Jwt:AccessTokenMinutes must be between 1 and 1440.");
            }

            if (refreshTokenDays <= 0 || refreshTokenDays > 365)
            {
                throw new InvalidOperationException(
                    "Jwt:RefreshTokenDays must be between 1 and 365.");
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

                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();

                            if (context.Response.HasStarted)
                            {
                                return;
                            }

                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/problem+json";

                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status401Unauthorized,
                                Title = "Authentication required.",
                                Detail = "A valid Bearer token is required for this endpoint.",
                                Instance = context.Request.Path
                            };

                            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                            problem.Extensions["code"] = "Unauthorized";

                            await context.Response.WriteAsJsonAsync(problem);
                        },

                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/problem+json";

                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status403Forbidden,
                                Title = "Access denied.",
                                Detail = "The authenticated user does not have permission to perform this action.",
                                Instance = context.Request.Path
                            };

                            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                            problem.Extensions["code"] = "Forbidden";

                            await context.Response.WriteAsJsonAsync(problem);
                        }
                    };
                });

            var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimit:AuthPermitLimit") ?? 10;
            var authWindowSeconds = builder.Configuration.GetValue<int?>("RateLimit:AuthWindowSeconds") ?? 60;

            if (authPermitLimit <= 0 || authPermitLimit > 1000 ||
                authWindowSeconds <= 0 || authWindowSeconds > 3600)
            {
                throw new InvalidOperationException(
                    "Authentication rate-limit settings are invalid.");
            }

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

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "Too many authentication requests. Please try again later.",
                        Instance = context.HttpContext.Request.Path
                    };

                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        problem,
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
                            PermitLimit = authPermitLimit,
                            Window = TimeSpan.FromSeconds(authWindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            });

            builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(
                    "database",
                    tags: new[] { "ready" });

            builder.Services.AddScoped<IMailHelper, MailHelper>();

            builder.Services.AddScoped<ILibraryService, LibraryService>();
            builder.Services.AddScoped<IWishlistService, WishlistService>();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IPurchaseService, PurchaseService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
            builder.Services.AddSingleton<IGameBuildStorageService, GameBuildStorageService>();

            builder.Services.AddTransient<SeedDb>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
                {
                    var dataContext = services.GetRequiredService<DataContext>();
                    await dataContext.Database.MigrateAsync();
                }

                var seedDb = services.GetRequiredService<SeedDb>();
                await seedDb.SeedAsync();
            }

            var cultureInfo = new CultureInfo("en-US");

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseMiddleware<ApiStatusCodeMiddleware>();

            app.UseHttpsRedirection();

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

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = HealthResponseWriter.WriteAsync
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("ready"),
                ResponseWriter = HealthResponseWriter.WriteAsync
            });

            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = HealthResponseWriter.WriteAsync
            });

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Games}/{action=Store}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
