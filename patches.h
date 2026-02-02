#pragma once
#include <windows.h>
#include <string>

// Patch definition structure
struct PnachPatch {
    std::wstring description;      // Unique comment to identify this patch
    std::wstring ntscPatch;       // NTSC patch code
    std::wstring palPatch;        // PAL patch code
    bool* enabledFlag;            // Pointer to config flag
};

// Patch management functions
bool ManagePnachPatches(const std::wstring& region, const std::wstring& pcsx2Path);

void SetBootToMultiplayer(bool enabled);
bool GetBootToMultiplayer();
void SetWideScreen(bool enabled);
bool GetWideScreen();
void SetProgressiveScan(bool enabled);
bool GetProgressiveScan();
