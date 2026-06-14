using System.IO.Compression;

namespace Platform.Api.Sdk;

/// <summary>
/// Packages the Godot GDScript SDK addon into a downloadable zip under <c>wwwroot/sdk/</c> at
/// startup, so developers can install it with the one-line installer (<c>/install.ps1</c> /
/// <c>/install.sh</c>) without cloning the repo. The zip's top-level entry is
/// <c>addons/multiplayer_service/…</c> so extracting it at a Godot project root yields
/// <c>res://addons/multiplayer_service</c>.
/// </summary>
internal static class SdkPackaging
{
    private const string AddonRelative = "addons/multiplayer_service";

    /// <summary>Builds (or rebuilds) the SDK zip. Idempotent; safe to call on every startup.</summary>
    public static void EnsureSdkPackage(IWebHostEnvironment env, ILogger logger)
    {
        var source = ResolveAddonSource(env.ContentRootPath);
        if (source is null)
        {
#pragma warning disable CA1848
            logger.LogWarning("Godot SDK addon source not found; the install download will be unavailable.");
#pragma warning restore CA1848
            return;
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var outDir = Path.Combine(webRoot, "sdk");
        var outPath = Path.Combine(outDir, "multiplayer_service.zip");

        try
        {
            Directory.CreateDirectory(outDir);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }

            using var zip = ZipFile.Open(outPath, ZipArchiveMode.Create);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file).Replace('\\', '/');

                // Don't ship local credentials; the editor dock / quickstart writes spawnweaver.cfg.
                if (rel.Equals("spawnweaver.cfg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                zip.CreateEntryFromFile(file, $"{AddonRelative}/{rel}", CompressionLevel.Optimal);
            }

#pragma warning disable CA1848
            logger.LogInformation("Packaged Godot SDK addon to {Path}", outPath);
#pragma warning restore CA1848
        }
        catch (IOException ex)
        {
#pragma warning disable CA1848
            logger.LogWarning(ex, "Could not package the Godot SDK zip.");
#pragma warning restore CA1848
        }
    }

    private static string? ResolveAddonSource(string contentRoot)
    {
        string[] candidates =
        [
            Path.Combine(contentRoot, "sdk-src", AddonRelative),                            // docker bundle
            Path.Combine(contentRoot, "..", "..", "sdk", "godot-gdscript", AddonRelative),  // repo (dotnet run)
        ];

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}
