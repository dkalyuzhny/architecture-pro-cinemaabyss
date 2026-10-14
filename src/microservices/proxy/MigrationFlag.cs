using Microsoft.Extensions.Primitives;

namespace CinemaAbyss.Proxy;

/// <summary>One validated snapshot per request; bad reloads keep the previous flag.</summary>
public sealed class MigrationFlag
{
    private sealed record Settings(bool Enabled, int MoviesPercent);
    private readonly IConfiguration configuration;
    private readonly ILogger<MigrationFlag> logger;
    private Settings current;

    public MigrationFlag(IConfiguration configuration, ILogger<MigrationFlag> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        current = Read();
    }

    public bool UseMovies()
    {
        var snapshot = Volatile.Read(ref current);
        return snapshot.Enabled && Random.Shared.Next(100) < snapshot.MoviesPercent;
    }

    private Settings Read()
    {
        var enabled = configuration.GetValue<bool>("Migration:Enabled");
        var percent = configuration.GetValue<int>("Migration:MoviesPercent");
        if (percent is < 0 or > 100)
            throw new InvalidOperationException("Migration:MoviesPercent must be between 0 and 100.");
        return new Settings(enabled, percent);
    }

}
