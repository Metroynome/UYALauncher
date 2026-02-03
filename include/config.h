#pragma once
#include <string>

// Path helpers
const std::wstring& GetConfigPath();

// Config access
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);

// State checks
bool IsFirstRun();
bool IsConfigComplete();
