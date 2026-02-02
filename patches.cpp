#include "patches.h"
#include <fstream>
#include <sstream>
#include <vector>
#include <iostream>
#include <Windows.h>
#include <ShlObj.h>

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
        &g_bootToMultiplayer
    },
    {
        L"// Enable Wide Screen",
        L"patch=1,EE,001439fd,extended,00000001\n",
        L"patch=1,EE,001439fd,extended,00000001\n",
        &g_wideScreen
    },
    {
        L"// Enable Progressive Scan in Multiplayer",
        L"patch=1,EE,d01d5524,extended,00000101\npatch=1,EE,201d5520,extended,00000001\n",
        L"// patch=1,EE,20381568,extended,080ED2C2\n",
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

// Main patch management function
bool ManagePnachPatches(const std::wstring& region, const std::wstring& pcsx2Path)
{
    std::wcout << L"Managing PCSX2 patches..." << std::endl;
    
    // Get PCSX2 directory
    size_t lastSlash = pcsx2Path.find_last_of(L"\\/");
    std::wstring pcsx2Dir = pcsx2Path.substr(0, lastSlash);
    
    // Location 1: Same directory as PCSX2 exe
    std::wstring patchesFolder1 = pcsx2Dir + L"\\patches";
    
    // Location 2: Documents folder
    wchar_t documentsPath[MAX_PATH];
    SHGetFolderPathW(NULL, CSIDL_PERSONAL, NULL, 0, documentsPath);
    std::wstring patchesFolder2 = std::wstring(documentsPath) + L"\\PCSX2\\patches";
    
    // Check which location exists
    std::wstring patchesFolder;
    DWORD attr1 = GetFileAttributesW(patchesFolder1.c_str());
    DWORD attr2 = GetFileAttributesW(patchesFolder2.c_str());
    
    bool location1Exists = (attr1 != INVALID_FILE_ATTRIBUTES && (attr1 & FILE_ATTRIBUTE_DIRECTORY));
    bool location2Exists = (attr2 != INVALID_FILE_ATTRIBUTES && (attr2 & FILE_ATTRIBUTE_DIRECTORY));
    
    if (location1Exists)
    {
        patchesFolder = patchesFolder1;
        std::wcout << L"Found patches folder: " << patchesFolder << std::endl;
    }
    else if (location2Exists)
    {
        patchesFolder = patchesFolder2;
        std::wcout << L"Found patches folder: " << patchesFolder << std::endl;
    }
    else
    {
        std::wcout << L"No patches folder found in either location" << std::endl;
        return false;
    }
    
    // Determine filename based on region
    std::wstring filename;
    std::wstring gameTitle;
    bool isPal = false;
    
    if (region == L"NTSC")
    {
        filename = L"SCUS-97353_45FE0CC4.pnach";
        gameTitle = L"Ratchet & Clank: Up Your Arsenal (NTSC-U)";
        isPal = false;
    }
    else if (region == L"PAL")
    {
        filename = L"SCES-52456_17125698.pnach";
        gameTitle = L"Ratchet & Clank 3 (PAL)";
        isPal = true;
    }
    else if (region == L"Both")
    {
        // Handle both regions
        bool ntscResult = ManagePnachPatches(L"NTSC", pcsx2Path);
        bool palResult = ManagePnachPatches(L"PAL", pcsx2Path);
        return ntscResult && palResult;
    }
    else
    {
        return false;
    }
    
    // Full path to pnach file
    std::wstring pnachPath = patchesFolder + L"\\" + filename;
    
    // Read existing file or prepare new one
    std::vector<std::wstring> fileLines;
    bool fileExists = (GetFileAttributesW(pnachPath.c_str()) != INVALID_FILE_ATTRIBUTES);
    
    if (fileExists)
    {
        // Read existing file
        std::wifstream readFile(pnachPath);
        if (readFile.is_open())
        {
            std::wstring line;
            while (std::getline(readFile, line))
            {
                fileLines.push_back(line);
            }
            readFile.close();
        }
    }
    else
    {
        // New file - add header
        fileLines.push_back(L"gametitle=" + gameTitle);
        fileLines.push_back(L"");
    }
    
    // Process each patch in our patch list
    for (int p = 0; p < g_patchCount; p++)
    {
        const PnachPatch& patch = g_patches[p];
        bool shouldExist = *(patch.enabledFlag);
        
        // Find if this patch exists in the file
        int patchStartLine = -1;
        int patchEndLine = -1;
        
        for (size_t i = 0; i < fileLines.size(); i++)
        {
            if (fileLines[i].find(patch.description) != std::wstring::npos)
            {
                patchStartLine = i;
                // Find end of this patch block
                for (size_t j = i + 1; j < fileLines.size(); j++)
                {
                    if (!fileLines[j].empty() && fileLines[j][0] != L'/' && fileLines[j].find(L"patch=") != std::wstring::npos)
                    {
                        // Found start of next patch
                        patchEndLine = j - 1;
                        break;
                    }
                    else if (j == fileLines.size() - 1)
                    {
                        // End of file
                        patchEndLine = j;
                        break;
                    }
                    else if (fileLines[j].find(L"patch=") != std::wstring::npos)
                    {
                        // This line is part of our patch
                        patchEndLine = j;
                    }
                }
                if (patchEndLine == -1) patchEndLine = patchStartLine;
                break;
            }
        }
        
        bool patchExists = (patchStartLine != -1);
        
        if (shouldExist && !patchExists)
        {
            // Add patch
            std::wcout << L"Adding patch: " << patch.description << std::endl;
            
            fileLines.push_back(L"");
            fileLines.push_back(patch.description);
            
            std::wstring patchCode = isPal ? patch.palPatch : patch.ntscPatch;
            std::wistringstream stream(patchCode);
            std::wstring line;
            while (std::getline(stream, line))
            {
                fileLines.push_back(line);
            }
        }
        else if (!shouldExist && patchExists)
        {
            // Remove patch
            std::wcout << L"Removing patch: " << patch.description << std::endl;
            
            // Remove lines from patchStartLine to patchEndLine
            fileLines.erase(fileLines.begin() + patchStartLine, fileLines.begin() + patchEndLine + 1);
            
            // Remove preceding empty line if it exists
            if (patchStartLine > 0 && patchStartLine <= fileLines.size() && fileLines[patchStartLine - 1].empty())
            {
                fileLines.erase(fileLines.begin() + patchStartLine - 1);
            }
        }
        else if (shouldExist && patchExists)
        {
            std::wcout << L"Patch already exists: " << patch.description << std::endl;
        }
    }
    
    // Write the file
    std::wofstream writeFile(pnachPath);
    if (!writeFile.is_open())
    {
        std::wcout << L"Failed to write pnach file: " << pnachPath << std::endl;
        return false;
    }
    
    for (const auto& line : fileLines)
    {
        writeFile << line << L"\n";
    }
    writeFile.close();
    
    std::wcout << L"Updated pnach file: " << pnachPath << std::endl;
    
    return true;
}
