using System.Collections.Generic;
using System.Linq;

namespace UYALauncher;

public static class GameSupport {
    private static readonly SupportedGameRule[] SupportedGames = {
        new(
            SupportedGame.Rac3,
            new[] { GameRegion.NTSC_U, GameRegion.PAL },
            "Ratchet and Clank: Up Your Arsenal NTSC-U/PAL",
            "Please select a Ratchet and Clank: Up Your Arsenal NTSC-U/PAL ISO (SCUS-97353 or SCES-52456)."),
        new(
            SupportedGame.Rac4,
            new[] { GameRegion.NTSC_U },
            "Ratchet: Deadlocked NTSC-U",
            "Please select a Ratchet: Deadlocked NTSC-U ISO (SCUS-97465).")
    };

    public static string? GetUnsupportedMessage(GameInfo? info) {
        if (info == null)
            return null;

        var rule = SupportedGames.FirstOrDefault(rule => rule.Game == info.Game);
        if (rule != null && rule.Regions.Contains(info.Region))
            return null;

        if (rule != null) {
            return BuildUnsupportedMessage(
                info,
                $"Only {rule.SupportedDescription} is supported right now.",
                rule.SelectionInstruction);
        }

        return BuildUnsupportedMessage(
            info,
            $"Only {BuildSupportedSummary()} are supported right now.",
            "Please select a supported RAC3/RAC4 ISO.");
    }

    private static string BuildSupportedSummary() {
        return string.Join(" and ", SupportedGames.Select(rule => rule.SupportedDescription));
    }

    private static string BuildUnsupportedMessage(GameInfo info, string header, string instruction) {
        var gameId = string.IsNullOrWhiteSpace(info.GameId) ? "unknown" : info.GameId;
        return $"{header}\n\n" +
               $"Detected: {info.GameLabel} {info.RegionLabel} ({gameId})\n" +
               instruction;
    }

    private sealed record SupportedGameRule(
        SupportedGame Game,
        IReadOnlySet<GameRegion> Regions,
        string SupportedDescription,
        string SelectionInstruction) {
        public SupportedGameRule(
            SupportedGame game,
            IEnumerable<GameRegion> regions,
            string supportedDescription,
            string selectionInstruction)
            : this(game, regions.ToHashSet(), supportedDescription, selectionInstruction) {
        }
    }
}
