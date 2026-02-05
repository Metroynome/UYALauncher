#pragma once
#include <windows.h>
#include <string>

struct SettingsState {
    bool cancelled = false;
    bool relaunch = false;
};

extern SettingsState settings;

// Show first-run setup dialog
bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent, bool hotkeyMode);
