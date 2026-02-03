#pragma once
#include <windows.h>
#include <string>

// Configuration management
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);
bool IsFirstRun();
bool IsConfigComplete();
void EnsureConfigDefaults();

// Configuration constants
extern const std::wstring CONFIG_FILE;