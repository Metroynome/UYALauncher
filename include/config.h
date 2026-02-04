#pragma once
#include <string>

struct LauncherConfig {
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring mapRegion;
    bool embedWindow;
    bool bootToMultiplayer;
    bool wideScreen;
    bool progressiveScan;
    bool showConsole;
};

const std::wstring& GetConfigPath();
LauncherConfig LoadConfig();
void SaveConfig(const LauncherConfig& config);
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);
bool IsFirstRun();
bool IsConfigComplete();
