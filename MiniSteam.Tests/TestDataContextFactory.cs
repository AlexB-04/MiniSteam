using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;

namespace MiniSteam.Tests;

internal static class TestDataContextFactory
{
    public static DataContext Create()
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase($"MiniSteamTests-{Guid.NewGuid()}")
            .Options;

        return new DataContext(options);
    }
}
