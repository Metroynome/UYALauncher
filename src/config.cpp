#include "config.h"
#include <Windows.h>
#include <shlobj.h>
#include "patches.h"
#include "updater.h"

Configuration config;
static std::wstring g_ConfigPath;

Configuration LoadConfig()
{
    Configuration config;
    config.isoPath = LoadConfigValue(L"ISO");
    config.pcsx2Path = LoadConfigValue(L"PCSX2");
    config.region = LoadConfigValue(L"Region");
    config.autoUpdate = (LoadConfigValue(L"AutoUpdate") == L"true");
    config.embedWindow = (LoadConfigValue(L"EmbedWindow") != L"false");
    config.bootToMultiplayer = (LoadConfigValue(L"BootToMultiplayer") == L"true");
    config.wideScreen = (LoadConfigValue(L"WideScreen") == L"true");
    config.progressiveScan = (LoadConfigValue(L"ProgressiveScan") == L"true");

    std::wstring showConsoleStr = LoadConfigValue(L"ShowConsole");
    config.showConsole = (!showConsoleStr.empty() && showConsoleStr == L"true");

    if (config.region.empty()) config.region = L"NTSC";

    std::wstring installedVersion = GetInstalledVersion();
    if (installedVersion.empty() || installedVersion == L"0.0.0") {
        SetInstalledVersion(UYA_LAUNCHER_VERSION);
        installedVersion = UYA_LAUNCHER_VERSION;
    }
    config.version = installedVersion;

    return config;
}


void SaveConfig(const Configuration& config)
{
    SaveConfigValue(L"ISO", config.isoPath);
    SaveConfigValue(L"PCSX2", config.pcsx2Path);
    SaveConfigValue(L"Region", config.region);
    SaveConfigValue(L"AutoUpdate", config.autoUpdate ? L"true" : L"false");
    SaveConfigValue(L"EmbedWindow", config.embedWindow ? L"true" : L"false");
    SaveConfigValue(L"BootToMultiplayer", config.bootToMultiplayer ? L"true" : L"false");
    SaveConfigValue(L"WideScreen", config.wideScreen ? L"true" : L"false");
    SaveConfigValue(L"ProgressiveScan", config.progressiveScan ? L"true" : L"false");
    SaveConfigValue(L"ShowConsole", config.showConsole ? L"true" : L"false");
}

void ApplyConfig(const Configuration& config)
{
    SetBootToMultiplayer(config.bootToMultiplayer);
    SetWideScreen(config.wideScreen);
    SetProgressiveScan(config.progressiveScan);
    ManagePnachPatches(config.region, config.pcsx2Path); 
}

const std::wstring& GetConfigPath()
{
    if (!g_ConfigPath.empty())
        return g_ConfigPath;

    wchar_t appDataPath[MAX_PATH];
    if (SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, appDataPath) != S_OK)
    {
        g_ConfigPath = L"config.ini";
        return g_ConfigPath;
    }

    std::wstring configDir = std::wstring(appDataPath) + L"\\UYALauncher";
    CreateDirectoryW(configDir.c_str(), NULL);

    g_ConfigPath = configDir + L"\\config.ini";
    return g_ConfigPath;
}

std::wstring LoadConfigValue(const std::wstring& key)
{
    wchar_t buffer[1024] = {0};
    GetPrivateProfileStringW(L"Settings", key.c_str(), L"", buffer, 1024, GetConfigPath().c_str());
    return std::wstring(buffer);
}

void SaveConfigValue(const std::wstring& key, const std::wstring& value)
{
    WritePrivateProfileStringW(L"Settings", key.c_str(), value.c_str(), GetConfigPath().c_str());
}

bool IsFirstRun()
{
    DWORD attrs = GetFileAttributesW(GetConfigPath().c_str());
    return (attrs == INVALID_FILE_ATTRIBUTES);
}

bool IsConfigComplete()
{
    if (LoadConfigValue(L"ISO").empty()) return false;
    if (LoadConfigValue(L"PCSX2").empty()) return false;
    if (LoadConfigValue(L"Region").empty()) return false;
    if (LoadConfigValue(L"EmbedWindow").empty()) return false;
    if (LoadConfigValue(L"BootToMultiplayer").empty()) return false;
    if (LoadConfigValue(L"WideScreen").empty()) return false;
    if (LoadConfigValue(L"ProgressiveScan").empty()) return false;

    return true;
}

std::wstring GetInstalledVersion()
{
    // Use AppData config path
    wchar_t path[MAX_PATH];
    SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, path);
    std::wstring iniPath = std::wstring(path) + L"\\UYALauncher\\config.ini";

    wchar_t version[32] = {0};
    GetPrivateProfileStringW(L"Launcher", L"Version", L"0.0.0", version, 32, iniPath.c_str());
    return std::wstring(version);
}

void SetInstalledVersion(const std::wstring& version)
{
    wchar_t path[MAX_PATH];
    SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, path);
    std::wstring iniPath = std::wstring(path) + L"\\UYALauncher\\config.ini";

    WritePrivateProfileStringW(L"Launcher", L"Version", version.c_str(), iniPath.c_str());
}

void InitializeVersion()
{
    std::wstring installedVersion = GetInstalledVersion();
    if (installedVersion.empty() || installedVersion == L"0.0.0") {
        SetInstalledVersion(UYA_LAUNCHER_VERSION);
    }
}