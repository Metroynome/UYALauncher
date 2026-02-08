#include "pcsx2.h"
#include "config.h"
#include "firstrun.h"
#include <iostream>
#include <thread>

// Global variables
PROCESS_INFORMATION g_pcsx2ProcessInfo = {0};
HWND g_pcsx2Window = NULL;
std::atomic<bool> g_pcsx2Running(false);

void InitializePCSX2Module()
{
    g_pcsx2ProcessInfo = {0};
    g_pcsx2Window = NULL;
    g_pcsx2Running = false;
}

bool LaunchPCSX2(const std::wstring& isoPath, const std::wstring& pcsx2Path, bool showConsole)
{
    if (showConsole)
        std::cout << "Launching PCSX2..." << std::endl;
    
    // set default arguments
    std::wstring arguments = L"";

    // ensure faster boot
    if (config.patches.bootToMultiplayer)
        arguments += L" -fastboot";

    // enable fullscreen
    if (config.fullscreen)
        arguments += L" -fullscreen";

    // Build command line: ("pcsx2.exe" [arguments] -- "path/to/iso")
    std::wstring cmdLine = L"\"" + pcsx2Path + L"\"" + arguments + L" -- \"" + isoPath + L"\"";

    STARTUPINFOW si = {0};
    si.cb = sizeof(si);
    
    // Launch PCSX2
    if (!CreateProcessW(NULL, &cmdLine[0], NULL, NULL, FALSE, 0, NULL, NULL, &si, &g_pcsx2ProcessInfo)) {
        DWORD error = GetLastError();
        if (showConsole)
            std::cout << "Failed to launch PCSX2. Error: " << error << std::endl;
        
        MessageBoxW(NULL, 
            L"Failed to launch PCSX2. Please check that PCSX2 path is correct in config.", 
            L"Error", MB_OK | MB_ICONERROR);
        return false;
    }

    if (showConsole)
        std::cout << "PCSX2 launched successfully (PID: " << g_pcsx2ProcessInfo.dwProcessId << ")" << std::endl;
    
    g_pcsx2Running = true;
    return true;
}

bool EmbedPCSX2Window(HWND parentWindow, bool showConsole)
{
    if (!parentWindow)
        return false;

    if (showConsole)
        std::cout << "Waiting for PCSX2 window..." << std::endl;

    // Wait for PCSX2 window to appear
    const int maxAttempts = 20;
    int attempts = 0;
    
    while (attempts < maxAttempts && g_pcsx2Window == NULL) {
        // Search for PCSX2 window by process ID
        struct EnumData {
            DWORD processId;
            HWND foundWindow;
        } data = { g_pcsx2ProcessInfo.dwProcessId, NULL };
        
        EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
            EnumData* pData = (EnumData*)lParam;
            DWORD windowProcessId;
            GetWindowThreadProcessId(hwnd, &windowProcessId);
            
            if (windowProcessId == pData->processId) {
                if (IsWindowVisible(hwnd)) {
                    char className[256];
                    GetClassNameA(hwnd, className, sizeof(className));
                    
                    // Filter out console and IME windows
                    if (strcmp(className, "ConsoleWindowClass") != 0 && strcmp(className, "IME") != 0) {
                        pData->foundWindow = hwnd;
                        return FALSE; // Stop enumeration
                    }
                }
            }
            return TRUE;
        }, (LPARAM)&data);
        
        g_pcsx2Window = data.foundWindow;
        
        if (g_pcsx2Window != NULL) {
            if (showConsole)
                std::cout << "Found PCSX2 window!" << std::endl;
            break;
        }
        
        Sleep(500);
        attempts++;
    }

    if (g_pcsx2Window != NULL) {
        if (showConsole) {
            std::cout << "Embedding PCSX2 window..." << std::endl;
            // Get current window info for debugging
            wchar_t windowTitle[256];
            char windowClass[256];
            GetWindowTextW(g_pcsx2Window, windowTitle, 256);
            GetClassNameA(g_pcsx2Window, windowClass, sizeof(windowClass));
            std::wcout << L"Window Title: " << windowTitle << std::endl;
            std::cout << "Window Class: " << windowClass << std::endl;
        }
        
        // Remove PCSX2 window border and make it a child of wrapper
        LONG_PTR style = GetWindowLongPtr(g_pcsx2Window, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU);
        style |= WS_CHILD;
        SetWindowLongPtr(g_pcsx2Window, GWL_STYLE, style);
        
        // Set wrapper as parent
        SetParent(g_pcsx2Window, parentWindow);

        // Resize to fit wrapper window
        RECT rect;
        GetClientRect(g_pcsx2Window, &rect);
        int windowWidth = rect.right - rect.left;
        int windowHeight = rect.bottom - rect.top;
        
        // Center the parent window on screen
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);
        int x = config.embedWindow ? ((screenWidth - windowWidth) / 2) : windowWidth;
        int y = config.embedWindow ? ((screenHeight - windowHeight) / 2) : windowHeight;
        
        SetWindowPos(parentWindow, NULL, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_FRAMECHANGED);
                
        UpdateWindow(g_pcsx2Window);

        // Show the window
        ShowWindow(g_pcsx2Window, SW_SHOW);

        if (showConsole)
            std::cout << "PCSX2 window embedded successfully!" << std::endl;
        
        return true;
    } else {
        if (showConsole)
            std::cout << "Could not find PCSX2 window after " << maxAttempts << " attempts." << std::endl;
        
        MessageBoxW(parentWindow, 
            L"Could not find PCSX2 window to embed.\n\n"
            L"PCSX2 is running but may appear in a separate window.\n\n"
            L"Try:\n"
            L"- Waiting a bit longer for PCSX2 to start\n"
            L"- Checking if PCSX2 launched successfully\n"
            L"- Looking for a separate PCSX2 window",
            L"Warning", MB_OK | MB_ICONWARNING);
        
        return false;
    }
}

bool IsPCSX2Running()
{
    if (!g_pcsx2ProcessInfo.hProcess)
        return false;

    DWORD exitCode;
    if (GetExitCodeProcess(g_pcsx2ProcessInfo.hProcess, &exitCode))
        return exitCode == STILL_ACTIVE;
    
    return false;
}

void MonitorPCSX2Process(HWND parentWindow, bool showConsole)
{
    if (showConsole)
        std::cout << "PCSX2 process monitor thread started." << std::endl;

    while (g_pcsx2Running)
    {
        if (g_pcsx2ProcessInfo.hProcess) {
            DWORD waitResult = WaitForSingleObject(g_pcsx2ProcessInfo.hProcess, 1000);
            if (waitResult == WAIT_OBJECT_0) {
                if (showConsole)
                    std::cout << "PCSX2 process has exited." << std::endl;
                
                g_pcsx2Running = false;
                
                // Close settings dialog if it's open by posting WM_CLOSE
                if (g_firstRunDlg && IsWindow(g_firstRunDlg))
                {
                    if (showConsole)
                        std::cout << "Closing settings dialog..." << std::endl;
                    PostMessage(g_firstRunDlg, WM_CLOSE, 0, 0);
                }
                
                // Post quit message to main window to exit message loop
                if (parentWindow)
                    PostMessage(parentWindow, WM_CLOSE, 0, 0);
                
                break;
            }
        }
        else
        {
            if (showConsole)
                std::cout << "No process handle, exiting monitor." << std::endl;
            break;
        }
    }

    if (showConsole)
        std::cout << "PCSX2 process monitor thread exiting." << std::endl;
}

void CleanupPCSX2()
{
    if (g_pcsx2ProcessInfo.hProcess) {
        DWORD exitCode;
        if (GetExitCodeProcess(g_pcsx2ProcessInfo.hProcess, &exitCode) && exitCode == STILL_ACTIVE) {
            TerminateProcess(g_pcsx2ProcessInfo.hProcess, 0);
            WaitForSingleObject(g_pcsx2ProcessInfo.hProcess, 2000);
        }
        CloseHandle(g_pcsx2ProcessInfo.hProcess);
        CloseHandle(g_pcsx2ProcessInfo.hThread);
        g_pcsx2ProcessInfo = {0};
    }
    
    g_pcsx2Window = NULL;
    g_pcsx2Running = false;
}

void TerminatePCSX2()
{
    if (g_pcsx2ProcessInfo.hProcess) {
        TerminateProcess(g_pcsx2ProcessInfo.hProcess, 0);
        WaitForSingleObject(g_pcsx2ProcessInfo.hProcess, 2000);
        CloseHandle(g_pcsx2ProcessInfo.hProcess);
        CloseHandle(g_pcsx2ProcessInfo.hThread);
        g_pcsx2ProcessInfo = {0};
    }
    
    g_pcsx2Window = NULL;
    g_pcsx2Running = false;
}
