#pragma once
#include <windows.h>
#include <string>

// Update custom maps from Horizon servers
// Downloads maps to uya/ folder next to the ISO file
// Returns true if successful, false on error
bool UpdateMaps(const std::string& isoPath, const std::string& region, HWND parentWindow);