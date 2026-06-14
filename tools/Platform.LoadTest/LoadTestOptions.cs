using System.Globalization;

namespace Platform.LoadTest;

/// <summary>Command-line options for the load test.</summary>
internal sealed class LoadTestOptions
{
    public string ApiBaseUrl { get; private set; } = "http://127.0.0.1:5000";
    public string? WebSocketUrl { get; private set; }
    public string? ProjectKey { get; private set; }
    public int Clients { get; private set; } = 20;
    public int RoomSize { get; private set; } = 4;
    public int Seconds { get; private set; } = 10;
    public double EventsPerSecond { get; private set; } = 5;

    /// <summary>WebSocket connect URL, derived from the API base if not given explicitly.</summary>
    public string ResolveWebSocketUrl()
    {
        if (!string.IsNullOrWhiteSpace(WebSocketUrl))
        {
            return WebSocketUrl!;
        }

        var ws = ApiBaseUrl
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        return ws + "/connect";
    }

    public static LoadTestOptions Parse(string[] args)
    {
        var options = new LoadTestOptions();

        for (var i = 0; i < args.Length - 1; i += 2)
        {
            var value = args[i + 1];
            switch (args[i])
            {
                case "--api": options.ApiBaseUrl = value; break;
                case "--url": options.WebSocketUrl = value; break;
                case "--key": options.ProjectKey = value; break;
                case "--clients": options.Clients = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--room-size": options.RoomSize = Math.Max(1, int.Parse(value, CultureInfo.InvariantCulture)); break;
                case "--seconds": options.Seconds = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--rate": options.EventsPerSecond = double.Parse(value, CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        return options;
    }

    public static string Usage =>
        """
        SpawnWeaver load test

        Usage: dotnet run --project tools/Platform.LoadTest -- [options]

          --api <url>         API base URL (default http://127.0.0.1:5000). Used to create a project.
          --key <pk_...>      Use an existing project public key instead of creating one.
          --url <ws://...>    WebSocket connect URL (default: derived from --api).
          --clients <n>       Number of concurrent clients (default 20).
          --room-size <n>     Players per room (default 4).
          --seconds <n>       Duration of the event-sending phase (default 10).
          --rate <n>          Game events per second per client (default 5).
        """;
}
