#include <windows.h>
#include <commdlg.h>
#include <shlobj.h>
#include <iostream>
#include <string>
#include <thread>
#include <atomic>
#include <fstream>
#include <sstream>
#include "firstrun.cpp"
#include "mapupdater.cpp"
#include "resource.h"

// Configuration
const std::wstring CONFIG_FILE = L"config.ini";

// Global variables for process management
PROCESS_INFORMATION processInfo = {0};
std::atomic<bool> running(false);
std::atomic<bool> shouldRestart(false);
HWND mainWindow = NULL;
HWND pcsx2Window = NULL;
bool consoleEnabled = false; // Global flag for console output

// Hotkey IDs
#define HOTKEY_RESTART 1
#define HOTKEY_SAVE_STATE 2
#define HOTKEY_LOAD_STATE 3
#define HOTKEY_SCREENSHOT 4
#define HOTKEY_EXIT 5
#define HOTKEY_CHANGE_ISO 6

// Function declarations
std::wstring LoadConfigValue(const std::wstring& key);
void SaveConfigValue(const std::wstring& key, const std::wstring& value);
std::wstring SelectISOFile();
bool IsFirstRun();
void PreLaunchSetup();
void LaunchPCSX2(const std::wstring& isoPath);
void EmbedPCSX2Window();
void RegisterHotkeys();
void UnregisterHotkeys();
void HandleHotkey(int hotkeyId);
void MonitorProcess();
void PostShutdownCleanup();
bool IsProcessRunning(HANDLE processHandle);
LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
BOOL WINAPI ConsoleHandler(DWORD signal);

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // Check if this is first run
    bool firstRun = IsFirstRun();
    
    if (firstRun)
    {
        // Show first-run setup dialog
        if (!ShowFirstRunDialog(hInstance, NULL))
        {
            // User cancelled
            return 0;
        }
        
        // Save configuration from first-run dialog
        SaveConfigValue(L"DefaultISO", g_firstRunConfig.isoPath);
        SaveConfigValue(L"PCSX2Path", g_firstRunConfig.pcsx2Path);
        SaveConfigValue(L"MapRegion", g_firstRunConfig.mapRegion);
        SaveConfigValue(L"EmbedWindow", g_firstRunConfig.embedWindow ? L"true" : L"false");
    }
    
    // Load configuration
    std::wstring isoPath = LoadConfigValue(L"DefaultISO");
    std::wstring pcsx2Path = LoadConfigValue(L"PCSX2Path");
    std::wstring mapRegion = LoadConfigValue(L"MapRegion");
    if (mapRegion.empty()) mapRegion = L"NTSC";
    
    std::wstring embedWindowStr = LoadConfigValue(L"EmbedWindow");
    bool embedWindow = (embedWindowStr != L"false"); // Default to true
    
    // Check if we should show console (from config file)
    std::wstring showConsole = LoadConfigValue(L"ShowConsole");
    consoleEnabled = (showConsole == L"true" || showConsole == L"True" || showConsole == L"1");
    
    if (consoleEnabled)
    {
        // Allocate a console for debugging
        AllocConsole();
        FILE* fDummy;
        freopen_s(&fDummy, "CONOUT$", "w", stdout);
        freopen_s(&fDummy, "CONOUT$", "w", stderr);
        
        std::cout << "UYA Launcher Starting..." << std::endl;
        std::cout << "Console enabled via config.ini" << std::endl;
    }
    
    // Only create window if embedding
    if (embedWindow)
    {
        // Create a wrapper window
        WNDCLASSEXW wc = {0};
        wc.cbSize = sizeof(WNDCLASSEXW);
        wc.lpfnWndProc = WndProc;
        wc.hInstance = hInstance;
        wc.lpszClassName = L"UYALauncherClass";
        wc.hbrBackground = (HBRUSH)GetStockObject(BLACK_BRUSH);
        wc.hCursor = LoadCursor(NULL, IDC_ARROW);
        wc.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));
        wc.hIconSm = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));        
        
        if (!RegisterClassExW(&wc))
        {
            return 0;
        }

        // Create window (larger default size for game display)
        mainWindow = CreateWindowExW(
            0,
            L"UYALauncherClass",
            L"Ratchet & Clank: Up Your Arsenal",
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT, CW_USEDEFAULT, 960, 720,
            NULL, NULL, hInstance, NULL
        );

        if (mainWindow == NULL)
        {
            return 0;
        }

        ShowWindow(mainWindow, nCmdShow);
        UpdateWindow(mainWindow);
        
        if (consoleEnabled)
            std::cout << "Main wrapper window created." << std::endl;

        // Register global hotkeys
        RegisterHotkeys();
    }

    // Pre-launch setup
    PreLaunchSetup();

    // Update custom maps if ISO is selected
    if (!isoPath.empty()) {
        // Convert wide string to narrow string for mapupdater
        std::string isoPathNarrow(isoPath.begin(), isoPath.end());
        std::string mapRegionNarrow(mapRegion.begin(), mapRegion.end());
        
        if (consoleEnabled)
            std::cout << "Checking for custom map updates..." << std::endl;
        
        UpdateMaps(isoPathNarrow, mapRegionNarrow, mainWindow);
    }

    // Launch PCSX2
    LaunchPCSX2(isoPath);

    if (embedWindow)
    {
        // Embed PCSX2 window
        EmbedPCSX2Window();

        // Start process monitor thread
        running = true;
        std::thread monitorThread(MonitorProcess);

        if (consoleEnabled)
            std::cout << "Entering message loop..." << std::endl;

        // Message loop
        MSG msg = {0};
        while (GetMessage(&msg, NULL, 0, 0))
        {
            if (msg.message == WM_HOTKEY)
            {
                HandleHotkey(msg.wParam);
            }
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        if (consoleEnabled)
            std::cout << "Exiting..." << std::endl;

        // Wait for monitor thread
        running = false;
        if (monitorThread.joinable())
            monitorThread.join();

        // Cleanup
        UnregisterHotkeys();
        PostShutdownCleanup();
        
        if (consoleEnabled)
            FreeConsole();

        return msg.wParam;
    }
    else
    {
        // Not embedding - just exit after launching PCSX2
        if (consoleEnabled)
        {
            std::cout << "PCSX2 launched. Launcher exiting..." << std::endl;
            FreeConsole();
        }
        
        return 0;
    }
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch(msg)
    {
        case WM_CLOSE:
            running = false;
            // Terminate PCSX2 when window closes
            if (processInfo.hProcess)
            {
                TerminateProcess(processInfo.hProcess, 0);
            }
            DestroyWindow(hwnd);
            break;
            
        case WM_DESTROY:
            PostQuitMessage(0);
            break;
            
        case WM_SIZE:
            // Resize embedded PCSX2 window when wrapper window is resized
            if (pcsx2Window != NULL && IsWindow(pcsx2Window))
            {
                RECT rect;
                GetClientRect(hwnd, &rect);
                SetWindowPos(pcsx2Window, NULL, 0, 0, 
                           rect.right - rect.left, 
                           rect.bottom - rect.top, 
                           SWP_NOZORDER | SWP_NOACTIVATE);
            }
            break;
            
        default:
            return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    return 0;
}

std::wstring LoadConfigValue(const std::wstring& key)
{
    std::wifstream file(CONFIG_FILE);
    if (!file.is_open())
        return L"";

    std::wstring line;
    std::wstring searchKey = key + L"=";
    
    while (std::getline(file, line))
    {
        if (line.empty() || line[0] == L';' || line[0] == L'#')
            continue;

        if (line.find(searchKey) == 0)
        {
            file.close();
            return line.substr(searchKey.length());
        }
    }

    file.close();
    return L"";
}

void SaveConfigValue(const std::wstring& key, const std::wstring& value)
{
    std::wifstream inFile(CONFIG_FILE);
    std::wstringstream buffer;
    std::wstring line;
    bool keyFound = false;
    std::wstring searchKey = key + L"=";

    if (inFile.is_open())
    {
        while (std::getline(inFile, line))
        {
            if (!keyFound && line.find(searchKey) == 0)
            {
                buffer << key << L"=" << value << L"\n";
                keyFound = true;
            }
            else
            {
                buffer << line << L"\n";
            }
        }
        inFile.close();
    }

    if (!keyFound)
    {
        buffer << key << L"=" << value << L"\n";
    }

    std::wofstream outFile(CONFIG_FILE);
    if (outFile.is_open())
    {
        outFile << buffer.str();
        outFile.close();
    }
}

std::wstring SelectISOFile()
{
    OPENFILENAMEW ofn;
    wchar_t szFile[MAX_PATH] = {0};

    ZeroMemory(&ofn, sizeof(ofn));
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = mainWindow;
    ofn.lpstrFile = szFile;
    ofn.nMaxFile = sizeof(szFile) / sizeof(wchar_t);
    ofn.lpstrFilter = L"PS2 ISO Files\0*.iso;*.bin;*.img\0All Files\0*.*\0";
    ofn.nFilterIndex = 1;
    ofn.lpstrFileTitle = NULL;
    ofn.nMaxFileTitle = 0;
    ofn.lpstrInitialDir = NULL;
    ofn.lpstrTitle = L"Select PS2 ISO/Game File";
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

    if (GetOpenFileNameW(&ofn) == TRUE)
    {
        return std::wstring(szFile);
    }

    return L"";
}

bool IsFirstRun()
{
    // Check if config file exists
    DWORD fileAttr = GetFileAttributesW(CONFIG_FILE.c_str());
    return (fileAttr == INVALID_FILE_ATTRIBUTES);
}

void PreLaunchSetup()
{
    // Config validation happens in WinMain now
    // This function kept for backwards compatibility
}

void LaunchPCSX2(const std::wstring& isoPath)
{
    if (consoleEnabled)
        std::cout << "Launching PCSX2..." << std::endl;
    
    // Get PCSX2 path from config
    std::wstring pcsx2Path = LoadConfigValue(L"PCSX2Path");
    if (pcsx2Path.empty())
    {
        if (consoleEnabled)
            std::cout << "PCSX2 path not configured!" << std::endl;
        ExitProcess(1);
    }
    
    STARTUPINFOW si = {0};
    si.cb = sizeof(si);

    std::wstring cmdLine = L"\"" + pcsx2Path + L"\"";
    
    if (!isoPath.empty())
    {
        cmdLine += L" \"" + isoPath + L"\"";
        if (consoleEnabled)
            std::wcout << L"Loading ISO: " << isoPath << std::endl;
    }

    if (consoleEnabled)
        std::wcout << L"Command line: " << cmdLine << std::endl;

    if (!CreateProcessW(
        NULL, &cmdLine[0], NULL, NULL, FALSE,
        0, NULL, NULL, &si, &processInfo))
    {
        DWORD error = GetLastError();
        if (consoleEnabled)
            std::cout << "Failed to launch PCSX2! Error code: " << error << std::endl;
        ExitProcess(1);
    }
    
    if (consoleEnabled)
        std::cout << "PCSX2 process started. PID: " << processInfo.dwProcessId << std::endl;
}

void EmbedPCSX2Window()
{
    if (consoleEnabled)
        std::cout << "Waiting for PCSX2 window to appear..." << std::endl;
    
    // Wait longer initially for PCSX2 to fully start
    Sleep(3000);

    pcsx2Window = NULL;
    int attempts = 0;
    int maxAttempts = 30; // Try for up to 15 seconds

    while (pcsx2Window == NULL && attempts < maxAttempts)
    {
        if (consoleEnabled)
            std::cout << "Attempt " << (attempts + 1) << "/" << maxAttempts << " - Searching for PCSX2 window..." << std::endl;
        
        // Method 1: Search by window title
        pcsx2Window = FindWindowW(NULL, L"PCSX2");
        
        // Method 2: Search for Qt5-based windows
        if (pcsx2Window == NULL)
            pcsx2Window = FindWindowW(L"Qt5QWindowIcon", NULL);
        
        // Method 3: Search for Qt6-based windows
        if (pcsx2Window == NULL)
            pcsx2Window = FindWindowW(L"Qt6QWindowIcon", NULL);
        
        // Method 4: Search for Qt5 with different class name
        if (pcsx2Window == NULL)
            pcsx2Window = FindWindowW(L"Qt5152QWindowIcon", NULL);
            
        // Method 5: Search for Qt6 with different class name
        if (pcsx2Window == NULL)
            pcsx2Window = FindWindowW(L"Qt6152QWindowIcon", NULL);
        
        // Method 6: Try to find any window owned by the PCSX2 process
        if (pcsx2Window == NULL && processInfo.dwProcessId != 0)
        {
            // Enumerate all top-level windows to find one owned by PCSX2
            struct EnumData {
                DWORD processId;
                HWND foundWindow;
            };
            
            EnumData data = { processInfo.dwProcessId, NULL };
            
            EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
                EnumData* pData = (EnumData*)lParam;
                DWORD windowProcessId;
                GetWindowThreadProcessId(hwnd, &windowProcessId);
                
                if (windowProcessId == pData->processId)
                {
                    // Check if this is a visible window (not hidden/system window)
                    if (IsWindowVisible(hwnd))
                    {
                        char className[256];
                        GetClassNameA(hwnd, className, sizeof(className));
                        
                        // Make sure it's not a console window or other system window
                        if (strcmp(className, "ConsoleWindowClass") != 0 && 
                            strcmp(className, "IME") != 0)
                        {
                            pData->foundWindow = hwnd;
                            return FALSE; // Stop enumeration
                        }
                    }
                }
                return TRUE; // Continue enumeration
            }, (LPARAM)&data);
            
            pcsx2Window = data.foundWindow;
        }
        
        if (pcsx2Window != NULL)
        {
            if (consoleEnabled)
                std::cout << "Found PCSX2 window!" << std::endl;
            break;
        }
        
        Sleep(500);
        attempts++;
    }

    if (pcsx2Window != NULL)
    {
        if (consoleEnabled)
        {
            std::cout << "Embedding PCSX2 window..." << std::endl;
            
            // Get current window info for debugging
            wchar_t windowTitle[256];
            char windowClass[256];
            GetWindowTextW(pcsx2Window, windowTitle, 256);
            GetClassNameA(pcsx2Window, windowClass, sizeof(windowClass));
            std::wcout << L"Window Title: " << windowTitle << std::endl;
            std::cout << "Window Class: " << windowClass << std::endl;
        }
        
        // Remove PCSX2 window border and make it a child of wrapper
        LONG_PTR style = GetWindowLongPtr(pcsx2Window, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU);
        style |= WS_CHILD;
        SetWindowLongPtr(pcsx2Window, GWL_STYLE, style);

        // Set wrapper as parent
        SetParent(pcsx2Window, mainWindow);

        // Resize to fit wrapper window
        RECT rect;
        GetClientRect(mainWindow, &rect);
        SetWindowPos(pcsx2Window, NULL, 0, 0, 
                   rect.right - rect.left, 
                   rect.bottom - rect.top, 
                   SWP_NOZORDER | SWP_FRAMECHANGED);

        // Show the window
        ShowWindow(pcsx2Window, SW_SHOW);
        UpdateWindow(pcsx2Window);

        if (consoleEnabled)
            std::cout << "PCSX2 window embedded successfully!" << std::endl;
    }
    else
    {
        if (consoleEnabled)
            std::cout << "Could not find PCSX2 window after " << maxAttempts << " attempts." << std::endl;
        MessageBoxW(mainWindow, 
            L"Could not find PCSX2 window to embed.\n\n"
            L"PCSX2 is running but may appear in a separate window.\n\n"
            L"Try:\n"
            L"- Waiting a bit longer for PCSX2 to start\n"
            L"- Checking if PCSX2 launched successfully\n"
            L"- Looking for a separate PCSX2 window",
            L"Warning", MB_OK | MB_ICONWARNING);
    }
}

void RegisterHotkeys()
{
    RegisterHotKey(NULL, HOTKEY_RESTART, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'R');
    RegisterHotKey(NULL, HOTKEY_SAVE_STATE, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'S');
    RegisterHotKey(NULL, HOTKEY_LOAD_STATE, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'L');
    RegisterHotKey(NULL, HOTKEY_SCREENSHOT, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'P');
    RegisterHotKey(NULL, HOTKEY_CHANGE_ISO, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'I');
    RegisterHotKey(NULL, HOTKEY_EXIT, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'Q');
}

void UnregisterHotkeys()
{
    UnregisterHotKey(NULL, HOTKEY_RESTART);
    UnregisterHotKey(NULL, HOTKEY_SAVE_STATE);
    UnregisterHotKey(NULL, HOTKEY_LOAD_STATE);
    UnregisterHotKey(NULL, HOTKEY_SCREENSHOT);
    UnregisterHotKey(NULL, HOTKEY_CHANGE_ISO);
    UnregisterHotKey(NULL, HOTKEY_EXIT);
}

void HandleHotkey(int hotkeyId)
{
    switch (hotkeyId)
    {
        case HOTKEY_RESTART:
            shouldRestart = true;
            TerminateProcess(processInfo.hProcess, 0);
            break;

        case HOTKEY_SAVE_STATE:
            // Send F1 to PCSX2 window
            if (pcsx2Window && IsWindow(pcsx2Window))
            {
                PostMessage(pcsx2Window, WM_KEYDOWN, VK_F1, 0);
                PostMessage(pcsx2Window, WM_KEYUP, VK_F1, 0);
            }
            break;

        case HOTKEY_LOAD_STATE:
            if (pcsx2Window && IsWindow(pcsx2Window))
            {
                PostMessage(pcsx2Window, WM_KEYDOWN, VK_F3, 0);
                PostMessage(pcsx2Window, WM_KEYUP, VK_F3, 0);
            }
            break;

        case HOTKEY_SCREENSHOT:
            if (pcsx2Window && IsWindow(pcsx2Window))
            {
                PostMessage(pcsx2Window, WM_KEYDOWN, VK_F8, 0);
                PostMessage(pcsx2Window, WM_KEYUP, VK_F8, 0);
            }
            break;

        case HOTKEY_CHANGE_ISO:
        {
            std::wstring newISO = SelectISOFile();
            if (!newISO.empty())
            {
                SaveConfigValue(L"DefaultISO", newISO);
                // ISO changed - restart will load new ISO
            }
            break;
        }

        case HOTKEY_EXIT:
            running = false;
            if (processInfo.hProcess)
            {
                TerminateProcess(processInfo.hProcess, 0);
            }
            PostQuitMessage(0);
            break;
    }
}

void MonitorProcess()
{
    while (running)
    {
        if (processInfo.hProcess)
        {
            DWORD waitResult = WaitForSingleObject(processInfo.hProcess, 1000);
            
            if (waitResult == WAIT_OBJECT_0)
            {
                if (shouldRestart)
                {
                    shouldRestart = false;
                    CloseHandle(processInfo.hProcess);
                    CloseHandle(processInfo.hThread);
                    Sleep(500);
                    std::wstring isoPath = LoadConfigValue(L"DefaultISO");
                    LaunchPCSX2(isoPath);
                    EmbedPCSX2Window(); // Re-embed after restart
                }
                else
                {
                    running = false;
                    PostQuitMessage(0);
                }
            }
        }
        else
        {
            break;
        }
    }
}

void PostShutdownCleanup()
{
    if (processInfo.hProcess)
    {
        DWORD exitCode;
        if (GetExitCodeProcess(processInfo.hProcess, &exitCode) && exitCode == STILL_ACTIVE)
        {
            TerminateProcess(processInfo.hProcess, 0);
            WaitForSingleObject(processInfo.hProcess, 2000);
        }
        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);
    }
}

bool IsProcessRunning(HANDLE processHandle)
{
    if (!processHandle)
        return false;

    DWORD exitCode;
    if (GetExitCodeProcess(processHandle, &exitCode))
    {
        return exitCode == STILL_ACTIVE;
    }
    return false;
}

BOOL WINAPI ConsoleHandler(DWORD signal)
{
    if (signal == CTRL_CLOSE_EVENT || signal == CTRL_C_EVENT || 
        signal == CTRL_BREAK_EVENT || signal == CTRL_LOGOFF_EVENT || 
        signal == CTRL_SHUTDOWN_EVENT)
    {
        if (processInfo.hProcess)
        {
            DWORD exitCode;
            if (GetExitCodeProcess(processInfo.hProcess, &exitCode) && exitCode == STILL_ACTIVE)
            {
                TerminateProcess(processInfo.hProcess, 0);
                WaitForSingleObject(processInfo.hProcess, 2000);
            }
            CloseHandle(processInfo.hProcess);
            CloseHandle(processInfo.hThread);
        }
        
        running = false;
        return TRUE;
    }
    return FALSE;
}
