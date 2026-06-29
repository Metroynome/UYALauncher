namespace UYALauncher;

public static class GameSupport {
    public static string? GetUnsupportedMessage(GameInfo? info) {
        if (info == null)
            return null;

        if (info.Game == SupportedGame.Deadlocked && info.Region != GameRegion.NTSC_U) {
            var gameId = string.IsNullOrWhiteSpace(info.GameId) ? "unknown" : info.GameId;
            return "Only Ratchet: Deadlocked NTSC-U is supported right now.\n\n" +
                   $"Detected: {info.GameLabel} {info.RegionLabel} ({gameId})\n" +
                   "Please select a Ratchet: Deadlocked NTSC-U ISO (SCUS-97465).";
        }

        return null;
    }
}