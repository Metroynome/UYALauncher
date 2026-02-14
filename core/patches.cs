using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UYALauncher;

public enum PatchTarget
{
    Singleplayer,
    Multiplayer
}

public class PnachPatch
{
    public required Func<PatchFlags, bool> IsEnabled { get; init; }
    public required PatchTarget Target { get; init; }
    public required string Description { get; init; }
    public string? NtscPatch { get; init; }
    public string? PalPatch { get; init; }
}

public static class PatchManager
{
    private const string NtscSpFile = "SCUS-97353_45FE0CC4.pnach";
    private const string NtscMpFile = "SCUS-97353_49536F3F.pnach";
    private const string PalSpFile = "SCES-52456_17125698.pnach";
    private const string PalMpFile = "SCES-52456_EDE8B391.pnach";

    private static readonly List<PnachPatch> Patches = new()
    {
        new PnachPatch
        {
            IsEnabled = p => p.BootToMultiplayer,
            Target = PatchTarget.Singleplayer,
            Description = "// UYA Launcher: Boot to Multiplayer",
            NtscPatch = "patch=1,EE,20381590,extended,080e6010\n",
            PalPatch = "patch=1,EE,20381568,extended,080ed2c2\n"
        },
        new PnachPatch
        {
            IsEnabled = p => p.Widescreen,
            Target = PatchTarget.Singleplayer,
            Description = "// UYA Launcher: Enable Widescreen",
            NtscPatch = "patch=1,EE,001439fd,extended,00000001\n",
            PalPatch = "patch=1,EE,001439fd,extended,00000001\n"
        },
        new PnachPatch
        {
            IsEnabled = p => p.ProgressiveScan,
            Target = PatchTarget.Singleplayer,
            Description = "// UYA Launcher: Enable Progressive Scan in Multiplayer",
            NtscPatch = "patch=1,EE,d01d5524,extended,00000101\npatch=1,EE,201d5520,extended,00000001\n",
            PalPatch = null
        },
        new PnachPatch
        {
            IsEnabled = _ => true, // Always enabled
            Target = PatchTarget.Multiplayer,
            Description = "// UYA Launcher: DNAS Skip",
            NtscPatch = null,
            PalPatch = "patch=1,EE,D04D55E4,extended,24020006\n" +
                       "patch=1,EE,004D55D8,extended,00000000\n" +
                       "patch=1,EE,D04D55E4,extended,24020006\n" +
                       "patch=1,EE,004D55E4,extended,00000005\n"
        }
    };

    public static void ApplyPatches(ConfigurationData config)
    {
        if (config.Region == "Both")
        {
            ManagePnachPatches(config, "NTSC");
            ManagePnachPatches(config, "PAL");
        }
        else
        {
            ManagePnachPatches(config, config.Region);
        }
    }

    private static void ManagePnachPatches(ConfigurationData config, string region)
    {
        var patchesFolder = FindPatchesFolder(config.Pcsx2Path);
        if (string.IsNullOrEmpty(patchesFolder))
        {
            Console.WriteLine("Patches folder not found!");
            return;
        }

        bool isPal = region == "PAL";

        foreach (var patch in Patches)
        {
            var (filename, gameTitle) = GetPatchFileInfo(region, patch.Target);
            var pnachPath = Path.Combine(patchesFolder, filename);
            var shouldExist = patch.IsEnabled(config.Patches);
            var patchCode = isPal ? patch.PalPatch : patch.NtscPatch;

            // Skip if no patch code for this region
            if (patchCode == null)
                continue;

            // Load or create file
            var fileLines = new List<string>();
            if (File.Exists(pnachPath))
            {
                fileLines.AddRange(File.ReadAllLines(pnachPath));
            }
            else
            {
                fileLines.Add($"gametitle={gameTitle}");
                fileLines.Add("");
            }

            // Find existing patch
            var patchStart = fileLines.FindIndex(line => line.Contains(patch.Description));
            var patchExists = patchStart >= 0;

            if (shouldExist && !patchExists)
            {
                // Add patch
                Console.WriteLine($"Adding patch: {patch.Description} to {filename}");
                fileLines.Add("");
                fileLines.Add(patch.Description);
                fileLines.AddRange(patchCode.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            else if (!shouldExist && patchExists)
            {
                // Remove patch
                Console.WriteLine($"Removing patch: {patch.Description} from {filename}");
                fileLines.RemoveAt(patchStart); // Remove description

                // Remove patch lines
                while (patchStart < fileLines.Count && 
                       fileLines[patchStart].TrimStart().StartsWith("patch="))
                {
                    fileLines.RemoveAt(patchStart);
                }

                // Remove preceding empty line if present
                if (patchStart > 0 && patchStart <= fileLines.Count && 
                    string.IsNullOrWhiteSpace(fileLines[patchStart - 1]))
                {
                    fileLines.RemoveAt(patchStart - 1);
                }
            }

            // Save file
            Directory.CreateDirectory(patchesFolder);
            File.WriteAllLines(pnachPath, fileLines, Encoding.UTF8);
        }
    }

    private static string? FindPatchesFolder(string pcsx2Path)
    {
        if (string.IsNullOrEmpty(pcsx2Path))
            return null;

        var pcsx2Dir = Path.GetDirectoryName(pcsx2Path);
        if (pcsx2Dir == null)
            return null;

        // Try PCSX2 installation directory
        var folder1 = Path.Combine(pcsx2Dir, "patches");
        if (Directory.Exists(folder1))
            return folder1;

        // Try Documents folder
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var folder2 = Path.Combine(documents, "PCSX2", "patches");
        if (Directory.Exists(folder2))
            return folder2;

        return null;
    }

    private static (string filename, string gameTitle) GetPatchFileInfo(string region, PatchTarget target)
    {
        return region switch
        {
            "NTSC" when target == PatchTarget.Singleplayer => 
                (NtscSpFile, "Ratchet & Clank: Up Your Arsenal Single Player (NTSC-U)"),
            "NTSC" when target == PatchTarget.Multiplayer => 
                (NtscMpFile, "Ratchet & Clank: Up Your Arsenal Multiplayer (NTSC-U)"),
            "PAL" when target == PatchTarget.Singleplayer => 
                (PalSpFile, "Ratchet & Clank 3 Single Player (PAL)"),
            "PAL" when target == PatchTarget.Multiplayer => 
                (PalMpFile, "Ratchet & Clank 3 Multiplayer (PAL)"),
            _ => throw new ArgumentException($"Invalid region/target: {region}/{target}")
        };
    }
}
