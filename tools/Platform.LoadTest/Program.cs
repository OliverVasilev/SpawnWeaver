using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Platform.LoadTest;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine(LoadTestOptions.Usage);
    return 0;
}

LoadTestOptions options;
try
{
    options = LoadTestOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(LoadTestOptions.Usage);
    return 1;
}

var projectKey = options.ProjectKey;
if (string.IsNullOrWhiteSpace(projectKey))
{
    Console.WriteLine($"Creating a project via {options.ApiBaseUrl} ...");
    projectKey = await CreateProjectAsync(options.ApiBaseUrl);
}

var wsUrl = options.ResolveWebSocketUrl();
Console.WriteLine($"Connecting {options.Clients} clients to {wsUrl} (rooms of {options.RoomSize})...");

var clients = new List<LoadClient>(options.Clients);
for (var i = 0; i < options.Clients; i++)
{
    clients.Add(new LoadClient(i));
}

using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var connectResults = await Task.WhenAll(clients.Select(async c =>
{
    try
    {
        await c.ConnectAsync(wsUrl, projectKey!, connectCts.Token);
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"client {c.Index} failed to connect: {ex.Message}");
        return false;
    }
}));

var connected = connectResults.Count(ok => ok);
Console.WriteLine($"Connected {connected}/{options.Clients} clients.");

// Group into rooms: the first client in each group creates, the rest join by code.
foreach (var group in clients.Chunk(options.RoomSize))
{
    var host = group[0];
    await host.CreateRoomAsync(connectCts.Token);
    string code;
    try
    {
        code = await host.RoomReady.WaitAsync(TimeSpan.FromSeconds(10));
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine($"host {host.Index} never got a room code; skipping group.");
        continue;
    }

    foreach (var member in group.Skip(1))
    {
        await member.JoinRoomAsync(code, connectCts.Token);
    }
}

// Wait for everyone to be in a room (best-effort).
await Task.WhenAll(clients.Select(async c =>
{
    try { await c.RoomReady.WaitAsync(TimeSpan.FromSeconds(10)); }
    catch (TimeoutException) { }
}));

var inRoom = clients.Count(c => c.RoomId is not null);
Console.WriteLine($"{inRoom}/{options.Clients} clients are in a room. Sending events for {options.Seconds}s...");

// Event phase.
long totalSent = 0;
using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.Seconds));
var interval = TimeSpan.FromSeconds(1.0 / options.EventsPerSecond);
var stopwatch = Stopwatch.StartNew();

var senders = clients.Select(client => Task.Run(async () =>
{
    while (!sendCts.IsCancellationRequested)
    {
        if (client.RoomId is null || client.Failed)
        {
            break;
        }

        try
        {
            await client.SendEventAsync(sendCts.Token);
            Interlocked.Increment(ref totalSent);
            await Task.Delay(interval, sendCts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (WebSocketException)
        {
            break;
        }
    }
})).ToArray();

await Task.WhenAll(senders);
stopwatch.Stop();

// Let any in-flight relays land, then close.
await Task.Delay(500);
await Task.WhenAll(clients.Select(c => c.CloseAsync()));

var totalReceived = clients.Sum(c => c.EventsReceived);
var failed = clients.Count(c => c.Failed);
var seconds = stopwatch.Elapsed.TotalSeconds;

Console.WriteLine();
Console.WriteLine("=== Load test results ===");
Console.WriteLine($"Clients connected : {connected}/{options.Clients}");
Console.WriteLine($"Clients in a room : {inRoom}");
Console.WriteLine($"Clients w/ errors : {failed}");
Console.WriteLine($"Duration          : {seconds:F1}s");
Console.WriteLine($"Events sent       : {totalSent} ({totalSent / seconds:F0}/s)");
Console.WriteLine($"Events received   : {totalReceived} ({totalReceived / seconds:F0}/s)");

foreach (var client in clients)
{
    client.Dispose();
}

return 0;

static async Task<string> CreateProjectAsync(string apiBaseUrl)
{
    using var http = new HttpClient();
    var response = await http.PostAsJsonAsync(
        $"{apiBaseUrl.TrimEnd('/')}/api/projects", new { name = "LoadTest" });
    response.EnsureSuccessStatusCode();

    var created = await response.Content.ReadFromJsonAsync<JsonElement>();
    return created.GetProperty("publicKey").GetString()
        ?? throw new InvalidOperationException("Project creation did not return a publicKey.");
}
