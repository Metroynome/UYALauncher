#pragma once
#include <windows.h>
#include <string>

enum class PatchTarget {
    singleplayer,
    multiplayer
};

// Patch flags struct - MUST be defined BEFORE config.h includes it
struct PatchFlags {
    bool bootToMultiplayer;
    bool wideScreen;
    bool progressiveScan;
};

struct PnachPatch {
    const wchar_t* description;
    const wchar_t* ntscPatch;
    const wchar_t* palPatch;
    PatchTarget target;
    bool* enabledFlag;
};

// Forward declare Configuration to avoid circular dependency
struct Configuration;

// Global patch state (DECLARE only, don't initialize here)
extern PatchFlags patch;

// Patch management functions
bool ManagePnachPatches(const std::wstring& region, const std::wstring& pcsx2Path);
void ApplyPatches(const Configuration& config);