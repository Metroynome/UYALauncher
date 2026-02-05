#pragma once
#include <string>

struct Configuration {
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring mapRegion;
    bool autoUpdate;
    bool embedWindow;
    bool bootToMultiplayer;
    bool wideScreen;
    bool progressiveScan;
    bool showConsole;
};

extern Configuration config;

const std::wstring& GetConfigPath();
Configuration LoadConfig();
void SaveConfig(const Configuration& config);
void ApplyConfig(const Configuration& config);
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);
bool IsFirstRun();
bool IsConfigComplete();
