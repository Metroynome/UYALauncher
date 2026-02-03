#include "config.h"
#include <fstream>
#include <sstream>

// Configuration file name
const std::wstring CONFIG_FILE = L"config.ini";

// Load a configuration value
std::wstring LoadConfigValue(const std::wstring& key)
{
    std::wifstream file(CONFIG_FILE);
    if (!file.is_open())
        return L"";

    std::wstring line;
    std::wstring searchKey = key + L"=";
    
    while (std::getline(file, line))
    {
        if (line.empty() || line[0] == L';' || line[0] == L'#')
            continue;

        if (line.find(searchKey) == 0)
        {
            file.close();
            return line.substr(searchKey.length());
        }
    }

    file.close();
    return L"";
}

// Save a configuration value
void SaveConfigValue(const std::wstring& key, const std::wstring& value)
{
    std::wifstream inFile(CONFIG_FILE);
    std::wstringstream buffer;
    std::wstring line;
    bool keyFound = false;
    std::wstring searchKey = key + L"=";

    if (inFile.is_open())
    {
        while (std::getline(inFile, line))
        {
            if (!keyFound && line.find(searchKey) == 0)
            {
                buffer << key << L"=" << value << L"\n";
                keyFound = true;
            }
            else
            {
                buffer << line << L"\n";
            }
        }
        inFile.close();
    }

    if (!keyFound)
    {
        buffer << key << L"=" << value << L"\n";
    }

    std::wofstream outFile(CONFIG_FILE);
    if (outFile.is_open())
    {
        outFile << buffer.str();
        outFile.close();
    }
}

// Check if this is first run (config doesn't exist)
bool IsFirstRun()
{
    DWORD fileAttr = GetFileAttributesW(CONFIG_FILE.c_str());
    return (fileAttr == INVALID_FILE_ATTRIBUTES);
}

// Check if all required config values exist
bool IsConfigComplete()
{
    std::wstring isoPath = LoadConfigValue(L"DefaultISO");
    std::wstring pcsx2Path = LoadConfigValue(L"PCSX2Path");
    std::wstring mapRegion = LoadConfigValue(L"MapRegion");
    std::wstring embedWindow = LoadConfigValue(L"EmbedWindow");
    std::wstring bootToMP = LoadConfigValue(L"BootToMultiplayer");
    std::wstring wideScreen = LoadConfigValue(L"WideScreen");
    std::wstring progressiveScan = LoadConfigValue(L"ProgressiveScan");
    
    if (isoPath.empty() || pcsx2Path.empty() || mapRegion.empty() || 
        embedWindow.empty() || bootToMP.empty() || wideScreen.empty() ||
        progressiveScan.empty())
    {
        return false;
    }
    
    return true;
}

// Add missing config values with defaults
void EnsureConfigDefaults()
{
    if (LoadConfigValue(L"MapRegion").empty())
        SaveConfigValue(L"MapRegion", L"NTSC");
    
    if (LoadConfigValue(L"EmbedWindow").empty())
        SaveConfigValue(L"EmbedWindow", L"true");
    
    if (LoadConfigValue(L"BootToMultiplayer").empty())
        SaveConfigValue(L"BootToMultiplayer", L"true");
    
    // if (LoadConfigValue(L"ShowConsole").empty())
    //     SaveConfigValue(L"ShowConsole", L"false");
}
