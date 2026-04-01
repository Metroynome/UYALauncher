using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;

namespace UYALauncher;

public enum GameRegion {
    NTSCU,
    PAL,
    Unknown
}

public class GameInfo {
    public string GameId { get; init; } = string.Empty;
    public string RawBootLine { get; init; } = string.Empty;
    public GameRegion Region { get; init; } = GameRegion.Unknown;
    public bool IsUYA { get; init; } = false;

    // Known UYA / R&C 3 disc IDs
    private const string NtscId = "SCUS-97353";
    private const string PalId  = "SCES-52456";

    public override string ToString() =>
        $"GameID={GameId}, Region={Region}, IsUYA={IsUYA}";
}

public static class GameDetector {
    // SYSTEM.CNF boot line looks like: BOOT2 = cdrom0:\SCUS_973.53;1
    // We extract the filename stem and normalise it to a disc ID (e.g. SCUS-97353).
    private static readonly Regex BootRegex = new(
        @"BOOT2\s*=\s*cdrom0:\\(?<file>[A-Z0-9_]+\.[A-Z0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Reads the ISO at <paramref name="isoPath"/> and returns a <see cref="GameInfo"/>
    /// describing the disc, or <c>null</c> if the file cannot be read or has no SYSTEM.CNF.
    /// </summary>
    public static GameInfo? ReadFromIso(string isoPath) {
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            return null;

        try {
            using var isoStream = File.OpenRead(isoPath);
            var cd = new CDReader(isoStream, joliet: true);

            // SYSTEM.CNF may appear with or without leading backslash depending on DiscUtils
            string? cnfContent = TryReadFile(cd, @"SYSTEM.CNF")
                               ?? TryReadFile(cd, @"\SYSTEM.CNF");

            if (cnfContent == null)
                return null;

            return ParseSystemCnf(cnfContent);
        } catch (Exception ex) {
            Console.WriteLine($"[GameDetector] Error reading ISO: {ex.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string? TryReadFile(CDReader cd, string path) {
        try {
            if (!cd.FileExists(path))
                return null;

            using var stream = cd.OpenFile(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            return reader.ReadToEnd();
        } catch {
            return null;
        }
    }

    private static GameInfo ParseSystemCnf(string cnfContent) {
        var bootLine = string.Empty;
        var gameId   = string.Empty;
        var region   = GameRegion.Unknown;

        var match = BootRegex.Match(cnfContent);
        if (match.Success) {
            bootLine = match.Value.Trim();

            // e.g. "SCUS_973.53" → "SCUS-97353"
            gameId = NormaliseGameId(match.Groups["file"].Value);
            region = RegionFromGameId(gameId);
        }

        bool isUya = gameId is "SCUS-97353" or "SCES-52456";

        return new GameInfo {
            GameId     = gameId,
            Region     = region,
        };
    }

    /// <summary>
    /// Converts a raw filename like "SCUS_973.53" or "SCES_524.56" to the
    /// canonical disc-ID format "SCUS-97353" / "SCES-52456".
    /// </summary>
    private static string NormaliseGameId(string raw) {
        // Split on underscore: ["SCUS", "973.53"]
        var parts = raw.Split('_', 2);
        if (parts.Length != 2)
            return raw.ToUpperInvariant();

        var prefix = parts[0].ToUpperInvariant();          // "SCUS"
        var digits = parts[1].Replace(".", string.Empty);  // "97353"

        return $"{prefix}-{digits}";
    }

    /// <summary>
    /// Infers region from the game-ID prefix used by Sony disc IDs.
    /// </summary>
    private static GameRegion RegionFromGameId(string gameId) {
        if (gameId.StartsWith("SCUS") || gameId.StartsWith("SLUS"))
            return GameRegion.NTSCU;

        if (gameId.StartsWith("SCES") || gameId.StartsWith("SLES"))
            return GameRegion.PAL;  // PAL / other non-NTSC-U

        return GameRegion.Unknown;
    }
}