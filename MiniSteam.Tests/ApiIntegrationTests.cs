using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MiniSteam.Data;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;

namespace MiniSteam.Tests;

public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_CanReachTestDatabase()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedLibrary_WithoutJwt_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/api/library",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(401, problem!.Status);
    }

    [Fact]
    public async Task MissingGame_ReturnsProblemDetailsWithTraceId()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/api/games/2147483647",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task PagedGames_ReturnsSeededPublicGameAndMetadata()
    {
        var gameName = $"Paged Game {Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            context.Games.Add(new Game
            {
                Name = gameName,
                Developer = "Integration Studio",
                ReleaseDate = DateTime.Today,
                IsPublic = true,
                Price = 9.99m
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = CreateClient();
        using var response = await client.GetAsync(
            $"/api/games/paged?searchString={Uri.EscapeDataString(gameName)}&page=1&pageSize=10",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<GameDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Page);
        Assert.Equal(10, result.PageSize);
        Assert.True(result.TotalItems >= 1);
        Assert.Contains(result.Items, item => item.Name == gameName);
    }

    [Fact]
    public async Task PagedGames_InvalidPage_ReturnsProblemDetails()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/api/games/paged?page=0&pageSize=20",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
    }

    [Fact]
    public async Task RefreshToken_IsRejected_AfterIdentitySecurityStampChanges()
    {
        using var client = CreateClient();

        var email = $"stamp-{Guid.NewGuid():N}@test.local";
        var password = "Strong123!";

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var tokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(tokens);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email);

            Assert.NotNull(user);

            var stampResult = await userManager.UpdateSecurityStampAsync(user!);
            Assert.True(stampResult.Succeeded);
        }

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = tokens!.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Register_Login_Me_AndRefresh_WorkAsDesktopSessionFlow()
    {
        using var client = CreateClient();

        var email = $"integration-{Guid.NewGuid():N}@test.local";
        var password = "Strong123!";

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registeredTokens = await registerResponse.Content
            .ReadFromJsonAsync<TokenResponseDto>(
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(registeredTokens);
        Assert.False(string.IsNullOrWhiteSpace(registeredTokens!.Token));
        Assert.False(string.IsNullOrWhiteSpace(registeredTokens.RefreshToken));

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginDto
            {
                Email = email,
                Password = password
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginTokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(loginTokens);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginTokens!.Token);

        using var meResponse = await client.GetAsync(
            "/api/auth/me",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = loginTokens.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshedTokens = await refreshResponse.Content
            .ReadFromJsonAsync<TokenResponseDto>(
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(refreshedTokens);
        Assert.NotEqual(registeredTokens.RefreshToken, refreshedTokens!.RefreshToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshedTokens.Token);

        using var revokeResponse = await client.PostAsJsonAsync(
            "/api/auth/revoke",
            new RevokeTokenRequestDto
            {
                RefreshToken = refreshedTokens.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;

        using var revokedRefreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = refreshedTokens.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task ReusingRotatedRefreshToken_RevokesReplacementSession()
    {
        using var client = CreateClient();

        var email = $"reuse-{Guid.NewGuid():N}@test.local";
        var password = "Strong123!";

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            },
            TestContext.Current.CancellationToken);

        var firstPair = await registerResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(firstPair);

        using var rotateResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = firstPair!.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        var secondPair = await rotateResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(secondPair);

        using var replayResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = firstPair.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var replacementResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequestDto
            {
                RefreshToken = secondPair!.RefreshToken
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replacementResponse.StatusCode);
    }
    [Fact]
    public async Task GameBuild_WithoutJwt_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            "/api/games/1/build",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GameBuild_NonOwner_ReturnsNotFound()
    {
        using var client = CreateClient();

        var email = $"build-nonowner-{Guid.NewGuid():N}@test.local";
        var password = "Strong123!";

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var tokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(tokens);

        int gameId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            var game = new Game
            {
                Name = $"Non-owned Build Game {Guid.NewGuid():N}",
                Developer = "Integration Studio",
                ReleaseDate = DateTime.Today,
                IsPublic = true,
                Price = 1m
            };

            context.Games.Add(game);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            gameId = game.Id;
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.Token);

        using var response = await client.GetAsync(
            $"/api/games/{gameId}/build",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GameBuild_Owner_CanReadMetadataAndDownloadArchive()
    {
        using var client = CreateClient();

        var email = $"build-owner-{Guid.NewGuid():N}@test.local";
        var password = "Strong123!";

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var tokens = await registerResponse.Content.ReadFromJsonAsync<TokenResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(tokens);

        string? archivePath = null;
        int gameId;

        try
        {
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

                var user = await userManager.FindByEmailAsync(email);
                Assert.NotNull(user);

                var game = new Game
                {
                    Name = $"Owned Build Game {Guid.NewGuid():N}",
                    Developer = "Integration Studio",
                    ReleaseDate = DateTime.Today,
                    IsPublic = true,
                    Price = 1m
                };

                context.Games.Add(game);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                gameId = game.Id;

                var buildDirectory = Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "GameBuilds");

                Directory.CreateDirectory(buildDirectory);

                var archiveFileName = $"test-{Guid.NewGuid():N}.zip";
                archivePath = Path.Combine(buildDirectory, archiveFileName);

                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    var executable = archive.CreateEntry("TestGame.exe");
                    await using var stream = executable.Open();
                    await stream.WriteAsync(
                        new byte[] { 0x4D, 0x5A, 0x00, 0x00 },
                        TestContext.Current.CancellationToken);
                }

                context.LibraryGames.Add(new LibraryGame
                {
                    UserId = user!.Id,
                    GameId = game.Id
                });

                context.GameBuilds.Add(new GameBuild
                {
                    GameId = game.Id,
                    Version = "1.0.0",
                    ArchiveFileName = archiveFileName,
                    ExecutablePath = "TestGame.exe",
                    FileSizeBytes = new FileInfo(archivePath).Length,
                    UpdatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens!.Token);

            using var metadataResponse = await client.GetAsync(
                $"/api/games/{gameId}/build",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);

            var build = await metadataResponse.Content.ReadFromJsonAsync<GameBuildDto>(
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(build);
            Assert.Equal(gameId, build!.GameId);
            Assert.Equal("1.0.0", build.Version);
            Assert.Equal("TestGame.exe", build.ExecutablePath);
            Assert.Equal(1, build.ArchiveFileCount);
            Assert.Equal(4, build.UncompressedSizeBytes);

            using var downloadResponse = await client.GetAsync(
                $"/api/games/{gameId}/build/download",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.Equal("application/zip", downloadResponse.Content.Headers.ContentType?.MediaType);
            Assert.True((await downloadResponse.Content.ReadAsByteArrayAsync(
                TestContext.Current.CancellationToken)).Length > 0);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(archivePath) && File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
    }

}
