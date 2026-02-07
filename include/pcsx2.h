#pragma once
#include <windows.h>
#include <string>
#include <atomic>
#include "config.h"

// Global state
extern PROCESS_INFORMATION g_pcsx2ProcessInfo;
extern HWND g_pcsx2Window;
extern std::atomic<bool> g_pcsx2Running;

void InitializePCSX2Module();
bool LaunchPCSX2(const std::wstring& isoPath, const std::wstring& pcsx2Path, bool showConsole = false);
bool EmbedPCSX2Window(HWND parentWindow, bool showConsole = false);
bool IsPCSX2Running();
void MonitorPCSX2Process(HWND parentWindow, bool showConsole = false);
void CleanupPCSX2();
void TerminatePCSX2();
