#pragma once
#include <windows.h>
#include <string>

// First run configuration structure
struct FirstRunConfig {
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring mapRegion;
    bool embedWindow;
    bool bootToMultiplayer;
    bool wideScreen;
    bool progressiveScan;
    bool cancelled;
};

// Global first-run config (accessible from main.cpp)
extern FirstRunConfig g_firstRunConfig;

// Show first-run setup dialog
bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent);