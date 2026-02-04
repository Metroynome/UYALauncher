#pragma once
#include <string>

struct BaseConfig {
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring mapRegion;
    bool embedWindow;
    bool bootToMultiplayer;
    bool wideScreen;
    bool progressiveScan;
    bool showConsole;
};

struct Configuration : BaseConfig {
    struct FirstRun : BaseConfig {
        bool cancelled = false;
    } firstRun;
};

extern Configuration config;

const std::wstring& GetConfigPath();
Configuration LoadConfig();
void SaveConfig(const Configuration& config);
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);
bool IsFirstRun();
bool IsConfigComplete();
