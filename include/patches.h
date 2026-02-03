#pragma once
#include <windows.h>
#include <string>

enum class PatchTarget {
    singleplayer,
    multiplayer
};

struct PnachPatch {
    const wchar_t* description;
    const wchar_t* ntscPatch;
    const wchar_t* palPatch;
    PatchTarget target;
    bool* enabledFlag;
};

// Patch management functions
bool ManagePnachPatches(const std::wstring& region, const std::wstring& pcsx2Path);

void SetBootToMultiplayer(bool enabled);
bool GetBootToMultiplayer();
void SetWideScreen(bool enabled);
bool GetWideScreen();
void SetProgressiveScan(bool enabled);
bool GetProgressiveScan();
