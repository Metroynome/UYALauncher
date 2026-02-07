#pragma once
#include <string>
#include "patches.h"

// Config key enum for type safety
enum class ConfigKey {
    iso,
    pcsx2,
    region,
    autoUpdate,
    embedWindow,
    bootToMultiplayer,
    wideScreen,
    progressiveScan,
    showConsole
};

// Config field type
enum class ConfigType {
    String,
    Bool,
    Float
};

// Config field info (used internally)
struct ConfigFieldInfo {
    ConfigKey key;
    const wchar_t* name;
    ConfigType type;
    void* fieldPtr;
};

struct Configuration {
    std::wstring version;
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring region;
    bool autoUpdate;
    bool embedWindow;
    bool showConsole;
    PatchFlags patches;  // Now this works!
};

extern Configuration config;

const std::wstring& GetConfigPath();
Configuration LoadConfig();
void SaveConfig(const Configuration& config);
void ApplyConfig(const Configuration& config);

// Type-safe config access
std::wstring LoadConfigValue(ConfigKey key);
void SaveConfigValue(ConfigKey key, const std::wstring& value);

// Legacy string-based access (for compatibility)
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);

bool IsFirstRun();
bool IsConfigComplete();
std::wstring GetInstalledVersion();
void SetInstalledVersion(const std::wstring& version);