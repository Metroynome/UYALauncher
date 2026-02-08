#pragma once
#include <windows.h>
#include <string>

struct SettingsState {
    bool cancelled = false;
    bool relaunch = false;
};

extern SettingsState settings;
extern HWND g_firstRunDlg;  // Add this line

// Show first-run setup dialog
bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent, bool hotkeyMode);
