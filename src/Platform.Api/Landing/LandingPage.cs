using System.Net;

namespace Platform.Api.Landing;

/// <summary>The public marketing landing page served at <c>/</c>. Auth-aware: the CTAs change
/// depending on whether the visitor is signed in.</summary>
internal static class LandingPage
{
    /// <summary>Renders the landing page for the current auth state.</summary>
    public static string Render(bool isAuthenticated, string? displayName)
    {
        string navRight, heroCtas, heroNote, finalCtas;

        if (isAuthenticated)
        {
            var who = string.IsNullOrWhiteSpace(displayName) ? "" : " as " + WebUtility.HtmlEncode(displayName);
            navRight =
                """<a href="/dashboard">Dashboard</a> <a class="btn btn-primary btn-sm" href="/dashboard/projects">Your projects</a>""";
            heroCtas =
                """<a class="btn btn-primary portal-glow" href="/dashboard">Open dashboard</a> <a class="btn btn-secondary" href="/dashboard/projects">Your projects</a>""";
            heroNote = $"Signed in{who} · Head to your dashboard to manage projects.";
            finalCtas =
                """<a class="btn btn-primary" href="/dashboard/projects">Go to your projects</a> <a class="btn btn-secondary" href="/dashboard/docs">Read the docs</a>""";
        }
        else
        {
            navRight =
                """<a href="/dashboard/signin">Sign in</a> <a class="btn btn-primary btn-sm" href="/dashboard/signup">Create account</a>""";
            heroCtas =
                """<a class="btn btn-primary portal-glow" href="/dashboard/signup">Create your account</a> <a class="btn btn-secondary" href="/dashboard/signin">Sign in</a>""";
            heroNote = "Free to start · Sign in to create a project and get your keys.";
            finalCtas =
                """<a class="btn btn-primary" href="/dashboard/signup">Create account</a> <a class="btn btn-secondary" href="/dashboard/signin">Sign in</a>""";
        }

        return Template
            .Replace("{{NAV_RIGHT}}", navRight, StringComparison.Ordinal)
            .Replace("{{HERO_CTAS}}", heroCtas, StringComparison.Ordinal)
            .Replace("{{HERO_NOTE}}", heroNote, StringComparison.Ordinal)
            .Replace("{{FINAL_CTAS}}", finalCtas, StringComparison.Ordinal);
    }

    private const string Template = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>SpawnWeaver — Multiplayer for Godot</title>
    <link rel="icon" type="image/png" href="/favicon.png" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Silkscreen:wght@400;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/spawnweaver.css" />
</head>
<body>
    <div class="landing">
        <div class="topbar">
            <a class="logo" href="/"><img src="/logo.png" alt="SpawnWeaver" /></a>
            <div class="nav-account">
                {{NAV_RIGHT}}
            </div>
        </div>

        <div class="hero">
            <img class="hero-logo" src="/logo.png" alt="SpawnWeaver" />
            <span class="pill">GODOT MULTIPLAYER</span>
            <h1>Online multiplayer for Godot, <span class="brand-gradient">without the backend</span>.</h1>
            <p class="sub">
                SpawnWeaver is a hosted multiplayer backend for Godot games. Drop in a native SDK and
                add rooms, lobbies, matchmaking, realtime events, and player saves with a few lines of
                GDScript — no servers to write, nothing to host.
            </p>
            <div class="cta-row">
                {{HERO_CTAS}}
            </div>
            <p class="muted" style="margin-top:10px;font-size:13px">{{HERO_NOTE}}</p>
        </div>

        <div class="card" style="margin-top:36px">
            <h2 style="margin-top:0">How it works</h2>
            <ol class="steps">
                <li><strong>Sign in &amp; create a project.</strong> Name it and you instantly get
                    your game's key.</li>
                <li><strong>Install the Godot SDK in one line.</strong> Run the installer from your
                    project root — it drops the addon into <code>res://addons</code>. Then enable the plugin.</li>
                <li><strong>Connect &amp; play.</strong> Paste your key into the editor dock,
                    write a few lines of GDScript, and run two instances to see two players together.</li>
            </ol>
            <pre># 1. install (from your Godot project folder)
iwr https://spawnweaver.dev/install.ps1 -UseBasicParsing | iex

# 2. connect in GDScript
MultiplayerService.connect_using_config()      # uses your saved key
MultiplayerService.create_room("Alice")        # share the room code
MultiplayerService.send_event("player_moved", { "x": 10, "y": 5 })</pre>
            <div class="cta-row" style="margin-top:18px">
                <a class="btn btn-primary" href="/dashboard/docs/getting-started">Read the quickstart</a>
                <a class="btn btn-secondary" href="/dashboard/docs">Browse the docs</a>
            </div>
        </div>

        <div class="card" style="margin-top:18px">
            <h2 style="margin-top:0">What you get</h2>
            <div class="tut-grid">
                <div class="tut"><div class="tut-title">🎮 Godot-native SDK</div><p class="muted">An autoload client with signals, guest auth, auto-reconnect, and clear errors — feels like part of Godot.</p></div>
                <div class="tut"><div class="tut-title">🚪 Rooms &amp; lobbies</div><p class="muted">Create or join by code, public lobby lists, ready checks, and host controls.</p></div>
                <div class="tut"><div class="tut-title">⚔️ Matchmaking</div><p class="muted">Queue players by mode and region; the server pairs them into a room.</p></div>
                <div class="tut"><div class="tut-title">📨 Realtime events</div><p class="muted">Relay game events to everyone in a room with a single call.</p></div>
                <div class="tut"><div class="tut-title">🔄 State sync</div><p class="muted">Shared room state and per-player entities, with snapshots for late joiners.</p></div>
                <div class="tut"><div class="tut-title">💾 Player saves</div><p class="muted">Store profiles, progression, and inventory with project-scoped key-value storage.</p></div>
            </div>
        </div>

        <div class="card" style="margin-top:18px;text-align:center">
            <h2 style="margin-top:0">Ready to add multiplayer?</h2>
            <p style="max-width:520px;margin:6px auto 0">
                Create a free account to get your keys and connect your first two players. Already
                have one? Sign in to pick up where you left off.
            </p>
            <div class="cta-row" style="justify-content:center;margin-top:18px">
                {{FINAL_CTAS}}
            </div>
        </div>

        <div class="card" style="margin-top:18px">
            <h2 style="margin-top:0">Feedback</h2>
            <p style="margin-top:4px">Found a bug or have a request? Tell us.</p>
            <input id="fb-email" class="field" type="email" placeholder="Email (optional)" />
            <textarea id="fb-message" class="field" rows="4" placeholder="Your feedback"></textarea>
            <div class="cta-row" style="margin:10px 0 0">
                <button class="btn btn-primary" onclick="sendFeedback()">Send feedback</button>
                <span id="fb-status" style="align-self:center"></span>
            </div>
        </div>

        <div class="footer">SpawnWeaver · multiplayer backend-as-a-service for Godot</div>
    </div>

    <script>
        async function sendFeedback() {
            const message = document.getElementById('fb-message').value.trim();
            const email = document.getElementById('fb-email').value.trim();
            const status = document.getElementById('fb-status');
            if (!message) { status.style.color = '#ff7a90'; status.textContent = 'Please enter a message.'; return; }
            try {
                const res = await fetch('/api/feedback', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email: email || null, message })
                });
                if (res.ok) {
                    status.style.color = '#54d6a0'; status.textContent = 'Thanks! 🚀';
                    document.getElementById('fb-message').value = '';
                } else {
                    status.style.color = '#ff7a90'; status.textContent = 'Could not send.';
                }
            } catch { status.style.color = '#ff7a90'; status.textContent = 'Could not send.'; }
        }
    </script>
</body>
</html>
""";
}
