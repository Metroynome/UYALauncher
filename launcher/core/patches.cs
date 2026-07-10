using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UYALauncher;

public enum PatchTarget {
    Singleplayer,
    Multiplayer,
    Shared
}

public class PnachPatch {
    public required Func<PatchFlags, bool> IsEnabled { get; init; }
    public required PatchTarget Target { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyDictionary<string, string> PatchCodes { get; init; }

    public string? GetPatchCode(PatchDiscProfile profile) {
        var patchKey = $"{profile.Game}:{profile.Region}:{profile.Version}:{Target}";
        return PatchCodes.TryGetValue(patchKey, out var code) ? code : null;
    }
}

public class PatchDiscProfile {
    public required SupportedGame Game { get; init; }
    public required GameRegion Region { get; init; }
    public required string Version { get; init; }
    public required PatchTarget Target { get; init; }
    public required string GameId { get; init; }
    public string? Checksum { get; init; }
    public required string GameTitle { get; init; }

    public string? Filename => Checksum == null ? null : $"{GameId}_{Checksum}.pnach";
}

public static class PatchManager {
    private const string Rac3NtscU = "Rac3:NTSC_U:1.00:";
    private const string Rac3Pal = "Rac3:PAL:1.00:";
    private const string Rac4NtscU = "Rac4:NTSC_U:1.00:";

    private static readonly List<PatchDiscProfile> DiscProfiles = new() {
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.NTSC_U,
            Version = "1.00",
            Target = PatchTarget.Singleplayer,
            GameId = "SCUS-97353",
            Checksum = "45FE0CC4",
            GameTitle = "Ratchet and Clank: Up Your Arsenal Single Player (NTSC-U)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.NTSC_U,
            Version = "1.00",
            Target = PatchTarget.Multiplayer,
            GameId = "SCUS-97353",
            Checksum = "49536F3F",
            GameTitle = "Ratchet and Clank: Up Your Arsenal Multiplayer (NTSC-U)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.PAL,
            Version = "1.00",
            Target = PatchTarget.Singleplayer,
            GameId = "SCES-52456",
            Checksum = "17125698",
            GameTitle = "Ratchet and Clank 3 Single Player (PAL)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.PAL,
            Version = "1.00",
            Target = PatchTarget.Multiplayer,
            GameId = "SCES-52456",
            Checksum = "EDE8B391",
            GameTitle = "Ratchet and Clank 3 Multiplayer (PAL)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.NTSC_J,
            Version = "1.03",
            Target = PatchTarget.Singleplayer,
            GameId = "SCPS-15084",
            Checksum = null,
            GameTitle = "Ratchet and Clank 3 Single Player (NTSC-J)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac3,
            Region = GameRegion.NTSC_J,
            Version = "1.03",
            Target = PatchTarget.Multiplayer,
            GameId = "SCPS-15084",
            Checksum = null,
            GameTitle = "Ratchet and Clank 3 Multiplayer (NTSC-J)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_U,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCUS-97465",
            Checksum = "9BFBCD42",
            GameTitle = "Ratchet: Deadlocked (NTSC-U)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.PAL,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCES-53285",
            Checksum = null,
            GameTitle = "Ratchet: Gladiator (PAL)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_K,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCPS-15099",
            Checksum = null,
            GameTitle = "Ratchet and Clank 4th Special Gift Package (NTSC-J alt)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_J,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCPS-15100",
            Checksum = null,
            GameTitle = "Ratchet and Clank 4th (NTSC-J The Best)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_A,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCAJ-20157",
            Checksum = null,
            GameTitle = "Ratchet and Clank 4th Special Gift Package (NTSC-Asia)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_K,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCKA-20060",
            Checksum = null,
            GameTitle = "Ratchet and Clank: Gonggu Jeonsa Wigi Ilbal (NTSC-K)"
        },
        new PatchDiscProfile {
            Game = SupportedGame.Rac4,
            Region = GameRegion.NTSC_K,
            Version = "1.00",
            Target = PatchTarget.Shared,
            GameId = "SCKA-20108",
            Checksum = null,
            GameTitle = "Ratchet and Clank: Gonggu Jeonsa Wigi Ilbal BigHit (NTSC-K)"
        },
    };

    private static readonly List<PnachPatch> Patches = new() {
        new PnachPatch {
            IsEnabled = p => p.BootToMultiplayer,
            Target = PatchTarget.Singleplayer,
            Description = "// Horizon Launcher: Boot to Multiplayer",
            PatchCodes = new Dictionary<string, string> {
                [Rac3NtscU + nameof(PatchTarget.Singleplayer)] =
                    "patch=1,EE,20381590,extended,080e6010\n",
                [Rac3Pal + nameof(PatchTarget.Singleplayer)] =
                    "patch=1,EE,20381568,extended,080ed2c2\n"
            }
        },
        new PnachPatch {
            IsEnabled = p => p.Widescreen,
            Target = PatchTarget.Singleplayer,
            Description = "// Horizon Launcher: Enable Widescreen",
            PatchCodes = new Dictionary<string, string> {
                [Rac3NtscU + nameof(PatchTarget.Singleplayer)] =
                    "patch=1,EE,001439fd,extended,00000001\n",
                [Rac3Pal + nameof(PatchTarget.Singleplayer)] =
                    "patch=1,EE,001439fd,extended,00000001\n"
            }
        },
        new PnachPatch{
            IsEnabled = _ => true,
            Target = PatchTarget.Multiplayer,
            Description = "// Horizon Launcher: DNAS Skip",
            PatchCodes = new Dictionary<string, string> {
                [Rac3Pal + nameof(PatchTarget.Multiplayer)] =
                    "patch=1,EE,D04D55E4,extended,24020006\n" +
                    "patch=1,EE,004D55D8,extended,00000000\n" +
                    "patch=1,EE,D04D55E4,extended,24020006\n" +
                    "patch=1,EE,004D55E4,extended,00000005\n"
            }
        },


        // Ratchet: Deadlocked patches
        new PnachPatch {
            IsEnabled = p => p.BootToMultiplayer,
            Target = PatchTarget.Shared,
            Description = "// Horizon Launcher: Boot to Multiplayer",
            PatchCodes = new Dictionary<string, string> {
                [Rac4NtscU + nameof(PatchTarget.Shared)] =
                    "patch=1,EE,D04385C8,extended,0000FF1B\n" +
                    "patch=1,EE,204385C8,extended,00000000\n"
            }
        },
        new PnachPatch {
            IsEnabled = _ => true,
            Target = PatchTarget.Shared,
            Description = "// Horizon Launcher: DNAS Skip",
            PatchCodes = new Dictionary<string, string> {
                [Rac4NtscU + nameof(PatchTarget.Shared)] =
                    "patch=1,EE,D0718e5c,extended,0000C33C\n" + 
                    "patch=1,EE,20718e5c,extended,0c1d4f1a\n"
            }
        },
        new PnachPatch {
            IsEnabled = _ => true,
            Target = PatchTarget.Shared,
            Description = "// Horizon Launcher: Disable Framelimiter",
            PatchCodes = new Dictionary<string, string> {
                [Rac4NtscU + nameof(PatchTarget.Shared)] =
                    "patch=1,EE,D021DF60,extended,0000001E\n" + 
                    "patch=1,EE,2021DF60,extended,0000003C\n"
            }
        }
    };

    public static void ApplyPatches(ConfigurationData config) {
        bool matchedAnyProfile = false;

        foreach (var isoPath in config.GetConfiguredIsoPaths()) {
            if (ApplyPatchesForIso(config, isoPath))
                matchedAnyProfile = true;
        }

        if (!matchedAnyProfile)
            Console.WriteLine("No patch profiles matched any configured ISO.");
    }

    public static void ApplyPatches(ConfigurationData config, SupportedGame game) {
        var isoPath = config.GetIsoPathForGame(game);
        if (string.IsNullOrWhiteSpace(isoPath)) {
            Console.WriteLine($"No ISO path configured for {game}; skipping patches.");
            return;
        }

        if (!ApplyPatchesForIso(config, isoPath))
            Console.WriteLine($"No patch profiles matched {isoPath}.");
    }

    private static bool ApplyPatchesForIso(ConfigurationData config, string isoPath) {
        var gameInfo = GameDetector.ReadFromIso(isoPath);
        var profiles = GetProfilesForConfig(gameInfo, config.Region);
        if (profiles.Count == 0) {
            Console.WriteLine($"No patch profiles matched {isoPath}.");
            return false;
        }

        ManagePnachPatches(config, profiles);
        return true;
    }

    private static void ManagePnachPatches(ConfigurationData config, IReadOnlyList<PatchDiscProfile> profiles) {
        var patchesFolder = Configuration.GetPatchesPath();

        if (!Directory.Exists(patchesFolder)) {
            Directory.CreateDirectory(patchesFolder);
            Console.WriteLine($"Created patches folder: {patchesFolder}");
        }

        Console.WriteLine($"Using patches folder: {patchesFolder}");

        foreach (var patch in Patches) {
            foreach (var profile in profiles.Where(profile => ProfileAppliesToPatch(profile, patch))) {
                var filename = profile.Filename;
                if (filename == null)
                    continue;

                var pnachPath = Path.Combine(patchesFolder, filename);
                var shouldExist = patch.IsEnabled(config.Patches);
                var patchCode = patch.GetPatchCode(profile);

                if (patchCode == null && !shouldExist)
                    continue;

                var fileLines = new List<string>();
                if (File.Exists(pnachPath)) {
                    fileLines.AddRange(File.ReadAllLines(pnachPath));
                } else {
                    fileLines.Add($"gametitle={profile.GameTitle}");
                    fileLines.Add("");
                }

                var patchStart = fileLines.FindIndex(line => line.Contains(patch.Description));
                var patchExists = patchStart >= 0;
                if (shouldExist && !patchExists) {
                    if (patchCode == null)
                        continue;

                    Console.WriteLine($"Adding patch: {patch.Description} to {filename}");
                    fileLines.Add("");
                    fileLines.Add(patch.Description);
                    fileLines.AddRange(patchCode.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                } else if (!shouldExist && patchExists) {
                    Console.WriteLine($"Removing patch: {patch.Description} from {filename}");
                    fileLines.RemoveAt(patchStart);

                    while (patchStart < fileLines.Count && fileLines[patchStart].TrimStart().StartsWith("patch=")) {
                        fileLines.RemoveAt(patchStart);
                    }

                    if (patchStart > 0 && patchStart <= fileLines.Count && string.IsNullOrWhiteSpace(fileLines[patchStart - 1])) {
                        fileLines.RemoveAt(patchStart - 1);
                    }
                }

                Directory.CreateDirectory(patchesFolder);
                File.WriteAllLines(pnachPath, fileLines, Encoding.UTF8);
            }
        }
    }

    private static List<PatchDiscProfile> GetProfilesForConfig(GameInfo? gameInfo, string configRegion) {
        if (gameInfo?.Game is SupportedGame.Rac3 or SupportedGame.Rac4) {
            return DiscProfiles
                .Where(profile => profile.Game == gameInfo.Game &&
                                  profile.Region == gameInfo.Region &&
                                  profile.GameId.Equals(gameInfo.GameId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var region = Configuration.NormalizeRegion(configRegion);
        if (region == "Both") {
            return DiscProfiles
                .Where(profile => profile.Game == SupportedGame.Rac3 &&
                                  profile.Region is GameRegion.NTSC_U or GameRegion.PAL)
                .ToList();
        }

        var gameRegion = region == "PAL" ? GameRegion.PAL : GameRegion.NTSC_U;
        return DiscProfiles
            .Where(profile => profile.Game == SupportedGame.Rac3 && profile.Region == gameRegion)
            .ToList();
    }

    private static bool ProfileAppliesToPatch(PatchDiscProfile profile, PnachPatch patch) {
        return profile.Target == PatchTarget.Shared || profile.Target == patch.Target;
    }
}
