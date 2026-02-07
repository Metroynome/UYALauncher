#include "config.h"
#include <Windows.h>
#include <shlobj.h>
#include <vector>
#include "patches.h"
#include "updater.h"

Configuration config;
static std::wstring ConfigPath;

static std::vector<ConfigFieldInfo> BuildConfigTable(Configuration& cfg)
{
    return {
        { ConfigKey::iso, L"ISO", ConfigType::String, &cfg.isoPath },
        { ConfigKey::pcsx2, L"PCSX2", ConfigType::String, &cfg.pcsx2Path },
        { ConfigKey::region, L"Region", ConfigType::String, &cfg.region },
        { ConfigKey::autoUpdate, L"AutoUpdate", ConfigType::Bool, &cfg.autoUpdate },
        { ConfigKey::embedWindow, L"EmbedWindow", ConfigType::Bool, &cfg.embedWindow },
        { ConfigKey::bootToMultiplayer, L"BootToMultiplayer", ConfigType::Bool, &cfg.patches.bootToMultiplayer },
        { ConfigKey::wideScreen, L"WideScreen", ConfigType::Bool, &cfg.patches.wideScreen },
        {ConfigKey::progressiveScan, L"ProgressiveScan", ConfigType::Bool, &cfg.patches.progressiveScan },
        { ConfigKey::showConsole,  L"ShowConsole", ConfigType::Bool, &cfg.showConsole },
    };
}

Configuration LoadConfig()
{
    Configuration cfg;
    auto fields = BuildConfigTable(cfg);
    for (const auto& field : fields) {
        std::wstring value = LoadConfigValue(field.name);
        switch (field.type) {
            case ConfigType::String:
                *(std::wstring*)field.fieldPtr = value;
                break;
            case ConfigType::Bool:
                *(bool*)field.fieldPtr = (value == L"true");
                break;
            case ConfigType::Float:
                *(float*)field.fieldPtr = std::stof(value);
                break;
        }
    }
    // Set defaults
    if (cfg.region.empty()) 
        cfg.region = L"NTSC";

    // Load version
    std::wstring installedVersion = GetInstalledVersion();
    if (installedVersion.empty() || installedVersion == L"0.0.0") {
        SetInstalledVersion(UYA_LAUNCHER_VERSION);
        installedVersion = UYA_LAUNCHER_VERSION;
    }
    cfg.version = installedVersion;

    return cfg;
}

void SaveConfig(const Configuration& cfg)
{
    // Cast away const to build field table
    Configuration& mutableCfg = const_cast<Configuration&>(cfg);
    auto fields = BuildConfigTable(mutableCfg);

    // Save all fields to config file
    for (const auto& field : fields) {
        std::wstring value;
        switch (field.type) {
            case ConfigType::String:
                value = *(std::wstring*)field.fieldPtr;
                break;
            case ConfigType::Bool:
                value = *(bool*)field.fieldPtr ? L"true" : L"false";
                break;
            case ConfigType::Float:
                value = std::to_wstring(*(float*)field.fieldPtr);
                break;
        }
        SaveConfigValue(field.name, value);
    }
}

void ApplyConfig(const Configuration& config)
{
    ApplyPatches(config);
}

const std::wstring& GetConfigPath()
{
    if (!ConfigPath.empty())
        return ConfigPath;

    wchar_t appDataPath[MAX_PATH];
    if (SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, appDataPath) != S_OK) {
        ConfigPath = L"config.ini";
        return ConfigPath;
    }

    std::wstring configDir = std::wstring(appDataPath) + L"\\UYALauncher";
    CreateDirectoryW(configDir.c_str(), NULL);

    ConfigPath = configDir + L"\\config.ini";
    return ConfigPath;
}

static const wchar_t* ConfigKeyToString(ConfigKey key)
{
    // Create a dummy config just to build the table
    Configuration dummy;
    auto fields = BuildConfigTable(dummy);
    for (const auto& field : fields) {
        if (field.key == key)
            return field.name;
    }
    
    return L"";
}

std::wstring LoadConfigValue(ConfigKey key)
{
    return LoadConfigValue(ConfigKeyToString(key));
}

void SaveConfigValue(ConfigKey key, const std::wstring& value)
{
    SaveConfigValue(ConfigKeyToString(key), value);
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
    Configuration dummy;
    auto fields = BuildConfigTable(dummy);
    
    // Check all fields except ShowConsole (which is optional)
    for (const auto& field : fields) {
        if (field.key == ConfigKey::showConsole)
            continue;
            
        if (LoadConfigValue(field.name).empty())
            return false;
    }
    
    return true;
}

std::wstring GetInstalledVersion()
{
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
    if (installedVersion.empty() || installedVersion == L"0.0.0")
        SetInstalledVersion(UYA_LAUNCHER_VERSION);
}