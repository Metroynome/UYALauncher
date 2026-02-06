#pragma once
#include <windows.h>
#include <string>
#include <atomic>

// PCSX2 process and window management

// Global state
extern PROCESS_INFORMATION g_pcsx2ProcessInfo;
extern HWND g_pcsx2Window;
extern std::atomic<bool> g_pcsx2Running;

// Initialize PCSX2 module
void InitializePCSX2Module();

// Launch PCSX2 with the specified ISO
bool LaunchPCSX2(const std::wstring& isoPath, const std::wstring& pcsx2Path, bool showConsole = false);

// Embed PCSX2 window into parent window
bool EmbedPCSX2Window(HWND parentWindow, bool showConsole = false);

// Check if PCSX2 process is running
bool IsPCSX2Running();

// Monitor PCSX2 process (call in separate thread)
void MonitorPCSX2Process(HWND parentWindow, bool showConsole = false);

// Cleanup PCSX2 process
void CleanupPCSX2();

// Terminate PCSX2 process
void TerminatePCSX2();
