#include "config.h"
#include <fstream>
#include <sstream>
#include <Windows.h>
#include <shlobj.h>

#include "config.h"
#include <fstream>
#include <sstream>
#include <Windows.h>
#include <shlobj.h>

static std::wstring g_ConfigPath;

LauncherConfig LoadConfig()
{
    LauncherConfig config;
    config.isoPath = LoadConfigValue(L"DefaultISO");
    config.pcsx2Path = LoadConfigValue(L"PCSX2Path");
    config.mapRegion = LoadConfigValue(L"MapRegion");
    config.embedWindow = (LoadConfigValue(L"EmbedWindow") != L"false");
    config.bootToMultiplayer = (LoadConfigValue(L"BootToMultiplayer") == L"true");
    config.wideScreen = (LoadConfigValue(L"WideScreen") == L"true");
    config.progressiveScan = (LoadConfigValue(L"ProgressiveScan") == L"true");
    config.showConsole = (LoadConfigValue(L"ShowConsole") == L"true");
    
    // showConsole is optional.  defaults to false.
    std::wstring showConsoleStr = LoadConfigValue(L"ShowConsole");
    config.showConsole = (!showConsoleStr.empty() && (showConsoleStr == L"true" || showConsoleStr == L"True" || showConsoleStr == L"1"));

    // Set defaults
    if (config.mapRegion.empty()) config.mapRegion = L"NTSC";
    
    return config;
}

void SaveConfig(const LauncherConfig& config)
{
    SaveConfigValue(L"DefaultISO", config.isoPath);
    SaveConfigValue(L"PCSX2Path", config.pcsx2Path);
    SaveConfigValue(L"MapRegion", config.mapRegion);
    SaveConfigValue(L"EmbedWindow", config.embedWindow ? L"true" : L"false");
    SaveConfigValue(L"BootToMultiplayer", config.bootToMultiplayer ? L"true" : L"false");
    SaveConfigValue(L"WideScreen", config.wideScreen ? L"true" : L"false");
    SaveConfigValue(L"ProgressiveScan", config.progressiveScan ? L"true" : L"false");
}

const std::wstring& GetConfigPath()
{
    if (!g_ConfigPath.empty())
        return g_ConfigPath;

    wchar_t appDataPath[MAX_PATH];
    if (SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, appDataPath) != S_OK)
    {
        g_ConfigPath = L"config.ini"; // fallback
        return g_ConfigPath;
    }

    std::wstring configDir = std::wstring(appDataPath) + L"\\UYALauncher";

    CreateDirectoryW(configDir.c_str(), NULL); // OK if exists

    g_ConfigPath = configDir + L"\\config.ini";
    return g_ConfigPath;
}


std::wstring LoadConfigValue(const std::wstring& key)
{
    std::wifstream file(GetConfigPath());
    if (!file.is_open())
        return L"";

    std::wstring line;
    const std::wstring searchKey = key + L"=";

    while (std::getline(file, line))
    {
        if (line.empty() || line[0] == L';' || line[0] == L'#')
            continue;

        if (line.rfind(searchKey, 0) == 0)
            return line.substr(searchKey.length());
    }

    return L"";
}

void SaveConfigValue(const std::wstring& key, const std::wstring& value)
{
    std::wifstream inFile(GetConfigPath());
    std::wstringstream buffer;
    std::wstring line;
    bool keyFound = false;
    const std::wstring searchKey = key + L"=";

    if (inFile.is_open())
    {
        while (std::getline(inFile, line))
        {
            if (!keyFound && line.rfind(searchKey, 0) == 0)
            {
                buffer << key << L"=" << value << L"\n";
                keyFound = true;
            }
            else
            {
                buffer << line << L"\n";
            }
        }
    }

    if (!keyFound)
        buffer << key << L"=" << value << L"\n";

    std::wofstream outFile(GetConfigPath());
    if (outFile.is_open())
        outFile << buffer.str();
}

bool IsFirstRun()
{
    DWORD attrs = GetFileAttributesW(GetConfigPath().c_str());
    return (attrs == INVALID_FILE_ATTRIBUTES);
}

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
