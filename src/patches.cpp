#include "patches.h"
#include <fstream>
#include <sstream>
#include <vector>
#include <iostream>
#include <Windows.h>
#include <ShlObj.h>

#define singleplayer PatchTarget::singleplayer
#define multiplayer PatchTarget::multiplayer

const std::wstring NTSC_SP_File = L"SCUS-97353_45FE0CC4.pnach";
const std::wstring NTSC_MP_File = L"SCUS-97353_49536F3F.pnach";
const std::wstring PAL_SP_File  = L"SCES-52456_17125698.pnach";
const std::wstring PAL_MP_File  = L"SCES-52456_EDE8B391.pnach";

// Global patch flags
static bool g_bootToMultiplayer = false;
static bool g_wideScreen = false;
static bool g_progressiveScan = false;

// Global patch list - ADD NEW PATCHES HERE!
static PnachPatch g_patches[] = {
    {
        L"// Boot to Multiplayer",
        L"patch=1,EE,20381590,extended,080e6010\n",
        L"patch=1,EE,20381568,extended,080ed2c2\n",
        singleplayer,
        &g_bootToMultiplayer
    },
    {
        L"// Enable Wide Screen",
        L"patch=1,EE,001439fd,extended,00000001\n",
        L"patch=1,EE,001439fd,extended,00000001\n",
        singleplayer,
        &g_wideScreen,
    },
    {
        L"// Enable Progressive Scan in Multiplayer",
        L"patch=1,EE,d01d5524,extended,00000101\npatch=1,EE,201d5520,extended,00000001\n",
        nullptr,
        singleplayer,
        &g_progressiveScan
    },
};

const int g_patchCount = sizeof(g_patches) / sizeof(g_patches[0]);

// Setters and getters
void SetBootToMultiplayer(bool enabled) {
    g_bootToMultiplayer = enabled;
}
bool GetBootToMultiplayer() {
    return g_bootToMultiplayer;
}

void SetWideScreen(bool enabled) {
    g_wideScreen = enabled;
}
bool GetWideScreen() {
    return g_wideScreen;
}

void SetProgressiveScan(bool enabled) {
    g_progressiveScan = enabled;
}
bool GetProgressiveScan() {
    return g_progressiveScan;
}

bool ManagePnachPatches(const std::wstring& region, const std::wstring& pcsx2Path)
{
    std::wcout << L"Managing PCSX2 patches..." << std::endl;

    // Determine patches folder
    size_t lastSlash = pcsx2Path.find_last_of(L"\\/"); 
    std::wstring pcsx2Dir = pcsx2Path.substr(0, lastSlash);

    std::wstring patchesFolder1 = pcsx2Dir + L"\\patches";

    wchar_t documentsPath[MAX_PATH];
    SHGetFolderPathW(NULL, CSIDL_PERSONAL, NULL, 0, documentsPath);
    std::wstring patchesFolder2 = std::wstring(documentsPath) + L"\\PCSX2\\patches";

    std::wstring patchesFolder;
    DWORD attr1 = GetFileAttributesW(patchesFolder1.c_str());
    DWORD attr2 = GetFileAttributesW(patchesFolder2.c_str());
    if (attr1 != INVALID_FILE_ATTRIBUTES && (attr1 & FILE_ATTRIBUTE_DIRECTORY))
        patchesFolder = patchesFolder1;
    else if (attr2 != INVALID_FILE_ATTRIBUTES && (attr2 & FILE_ATTRIBUTE_DIRECTORY))
        patchesFolder = patchesFolder2;
    else
    {
        std::wcout << L"No patches folder found!" << std::endl;
        return false;
    }

    // Handle "Both" regions recursively
    if (region == L"Both")
    {
        bool ntscResult = ManagePnachPatches(L"NTSC", pcsx2Path);
        bool palResult  = ManagePnachPatches(L"PAL", pcsx2Path);
        return ntscResult && palResult;
    }

    bool isPal = (region == L"PAL");

    for (int p = 0; p < g_patchCount; p++)
    {
        const PnachPatch& patch = g_patches[p];
        bool shouldExist = *(patch.enabledFlag);

        // Determine correct filename and game title
        std::wstring filename, gameTitle;
        if (region == L"NTSC")
        {
            if (patch.target == singleplayer)
            {
                filename = NTSC_SP_File;
                gameTitle = L"Ratchet & Clank: Up Your Arsenal Single Player (NTSC-U)";
            }
            else // Multiplayer
            {
                filename = NTSC_MP_File;
                gameTitle = L"Ratchet & Clank: Up Your Arsenal Multiplayer (NTSC-U)";
            }
        }
        else // PAL
        {
            if (patch.target == singleplayer)
            {
                filename = PAL_SP_File;
                gameTitle = L"Ratchet & Clank 3 Single Player (PAL)";
            }
            else // Multiplayer
            {
                filename = PAL_MP_File;
                gameTitle = L"Ratchet & Clank 3 Multiplayer (PAL)";
            }
        }

        std::wstring pnachPath = patchesFolder + L"\\" + filename;
        std::vector<std::wstring> fileLines;

        // Load existing file or create header
        bool fileExists = (GetFileAttributesW(pnachPath.c_str()) != INVALID_FILE_ATTRIBUTES);
        if (fileExists)
        {
            std::wifstream readFile(pnachPath);
            if (readFile.is_open())
            {
                std::wstring line;
                while (std::getline(readFile, line))
                    fileLines.push_back(line);
                readFile.close();
            }
        }
        else
        {
            fileLines.push_back(L"gametitle=" + gameTitle);
            fileLines.push_back(L"");
        }

        // Determine which patch code to use
        const wchar_t* patchCodePtr = isPal ? patch.palPatch : patch.ntscPatch;

        // Skip patch if no code for this region
        if (!patchCodePtr)
            continue;

        std::wstring patchCode = patchCodePtr;

        // Search for patch block
        size_t patchStart = fileLines.size();
        for (size_t i = 0; i < fileLines.size(); i++)
        {
            if (fileLines[i].find(patch.description) != std::wstring::npos)
            {
                patchStart = i;
                break;
            }
        }

        bool patchExists = (patchStart < fileLines.size());

        if (shouldExist && !patchExists)
        {
            // Add patch
            std::wcout << L"Adding patch: " << patch.description << L" to " << filename << std::endl;
            fileLines.push_back(L"");
            fileLines.push_back(patch.description);

            std::wistringstream stream(patchCode);
            std::wstring line;
            while (std::getline(stream, line))
                if (!line.empty())
                    fileLines.push_back(line);
        }
        else if (!shouldExist && patchExists)
        {
            // Remove patch
            std::wcout << L"Removing patch: " << patch.description << L" from " << filename << std::endl;

            size_t i = patchStart;
            fileLines.erase(fileLines.begin() + i); // remove description line

            // Remove following patch lines
            while (i < fileLines.size())
            {
                std::wstring line = fileLines[i];
                line.erase(0, line.find_first_not_of(L" \t"));
                if (!line.empty() && line.find(L"patch=") == 0)
                    fileLines.erase(fileLines.begin() + i);
                else
                    break;
            }

            // Remove preceding empty line
            if (i > 0 && i <= fileLines.size() && fileLines[i - 1].empty())
                fileLines.erase(fileLines.begin() + i - 1);
        }
        else if (shouldExist && patchExists)
        {
            std::wcout << L"Patch already exists: " << patch.description << L" in " << filename << std::endl;
        }

        // Save file
        std::wofstream writeFile(pnachPath);
        if (!writeFile.is_open())
        {
            std::wcout << L"Failed to write pnach file: " << pnachPath << std::endl;
            continue;
        }

        for (const auto& line : fileLines)
            writeFile << line << L"\n";

        writeFile.close();
        std::wcout << L"Updated pnach file: " << pnachPath << std::endl;
    }

    return true;
}


