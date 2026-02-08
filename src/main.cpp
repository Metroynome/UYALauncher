#include <windows.h>
#include <commdlg.h>
#include <shlobj.h>
#include <iostream>
#include <string>
#include <thread>
#include "firstrun.h"
#include "config.h"
#include "mapupdater.h"
#include "resource.h"
#include "patches.h"
#include "updater.h"
#include "pcsx2.h"

// Hotkeys
#define HOTKEY_UPDATE_MAPS  1
#define HOTKEY_OPEN_FIRSTRUN  2

// Global variables
HWND mainWindow = NULL;
bool consoleEnabled = false;
static bool justUpdated = false;

// Function declarations
void RegisterHotkeys();
void UnregisterHotkeys();
void HandleHotkey(int hotkeyId);
LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
BOOL WINAPI ConsoleHandler(DWORD signal);

// Helper function to convert wstring to string
std::string WStringToString(const std::wstring& wstr) {
    if (wstr.empty()) return std::string();
    int size = WideCharToMultiByte(CP_UTF8, 0, wstr.c_str(), -1, nullptr, 0, nullptr, nullptr);
    std::string result(size - 1, 0);
    WideCharToMultiByte(CP_UTF8, 0, wstr.c_str(), -1, &result[0], size, nullptr, nullptr);
    return result;
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // Handle self-update command line argument
    if (wcsstr(GetCommandLineW(), L"--self-update")){
        std::wstring cmdLine = GetCommandLineW();
        size_t pos = cmdLine.find(L"--self-update");
        if (pos != std::wstring::npos) {
            std::wstring args = cmdLine.substr(pos + 14); // skip "--self-update "
            args.erase(0, args.find_first_not_of(L" \""));
            args.erase(args.find_last_not_of(L" \"") + 1);

            // Format: "<newExePath>|<remoteVersion>"
            size_t sep = args.find(L"|");
            std::wstring newExePath;
            std::wstring remoteVersion;
            if (sep != std::wstring::npos) {
                newExePath = args.substr(0, sep);
                remoteVersion = args.substr(sep + 1);
            } else {
                newExePath = args;
                remoteVersion = L"0.0.0";
            }
            RunSelfUpdate(newExePath, remoteVersion);
        }
        return 0;
    }

    // Initialize PCSX2 module
    InitializePCSX2Module();

    // Check if this is first run OR if config is incomplete
    bool firstRun = IsFirstRun();
    bool configIncomplete = !firstRun && !IsConfigComplete();

    // Check for updates at start
    if (!firstRun && !justUpdated) {
        Configuration tempConfig = LoadConfig();
        if (tempConfig.autoUpdate) {
            RunUpdater(true);
        }
    }

    // Show setup dialog if needed
    if (firstRun || configIncomplete) {
        if (configIncomplete && consoleEnabled)
            std::cout << "Incomplete config detected - showing setup dialog" << std::endl;
        
        if (!ShowFirstRunDialog(hInstance, NULL, false))
            return 0;
    }
    
    // Load configuration
    config = LoadConfig();
    std::wstring isoPath = config.isoPath;
    std::wstring pcsx2Path = config.pcsx2Path;
    std::wstring mapRegion = config.region;
    bool embedWindow = config.embedWindow;
    consoleEnabled = config.showConsole;
    
    // Apply Config
    ApplyConfig(config);
    
    // Setup console if enabled
    if (consoleEnabled) {
        AllocConsole();
        FILE* fDummy;
        freopen_s(&fDummy, "CONOUT$", "w", stdout);
        freopen_s(&fDummy, "CONOUT$", "w", stderr);
        
        // Set console title
        SetConsoleTitleW(L"UYA Launcher Console");
        
        // Register console control handler
        SetConsoleCtrlHandler(ConsoleHandler, TRUE);
        
        std::cout << "UYA Launcher Starting..." << std::endl;
        std::cout << "Console enabled via config.ini" << std::endl;
    }
    
    // Always create a window (for message pump and hotkeys)
    // If not embedding, create hidden message-only window
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
        return 0;

    if (embedWindow) {
        // Calculate centered position
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);
        int windowWidth = 960;
        int windowHeight = 720;
        int x = (screenWidth - windowWidth) / 2;
        int y = (screenHeight - windowHeight) / 2;
        
        // Create visible window for embedding - but start hidden
        mainWindow = CreateWindowExW(0, L"UYALauncherClass", L"UYA Launcher", WS_OVERLAPPEDWINDOW, x, y, windowWidth, windowHeight, NULL, NULL, hInstance, NULL);
        if (mainWindow == NULL)
            return 0;
        
        UpdateWindow(mainWindow);

        // don't show window yet.

        if (consoleEnabled)
            std::cout << "Main wrapper window created (hidden until embedding)." << std::endl;
    } else {
        // Create hidden message-only window for hotkeys
        mainWindow = CreateWindowExW(0, L"UYALauncherClass", L"UYA Launcher", WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 1, 1, HWND_MESSAGE, NULL, hInstance, NULL);
        if (mainWindow == NULL)
            return 0;
        
        if (consoleEnabled)
            std::cout << "Hidden message window created for hotkeys." << std::endl;
    }
    
    // Register global hotkeys
    RegisterHotkeys();

    // Update custom maps if ISO is selected
    if (!isoPath.empty()) {
        std::string isoPathNarrow = WStringToString(isoPath);
        std::string mapRegionNarrow = WStringToString(mapRegion);
        
        if (consoleEnabled)
            std::cout << "Checking for custom map updates..." << std::endl;
        
        UpdateMaps(isoPathNarrow, mapRegionNarrow, mainWindow);
    }

    // Launch PCSX2
    if (!LaunchPCSX2(isoPath, pcsx2Path, consoleEnabled)) {
        if (consoleEnabled)
            FreeConsole();
        return 1;
    }

    // Embed PCSX2 window if in embed mode
    if (embedWindow) {
        // Hide the parent window before embedding
        ShowWindow(mainWindow, SW_HIDE);
        
        EmbedPCSX2Window(mainWindow, consoleEnabled);
        
        // Show the parent window after embedding is complete
        ShowWindow(mainWindow, SW_SHOW);
    }

    // Start process monitor thread
    std::thread monitorThread(MonitorPCSX2Process, mainWindow, consoleEnabled);
    if (consoleEnabled)
        std::cout << "Entering message loop..." << std::endl;

    // Message loop (works for both embed and non-embed modes)
    MSG msg = {0};
    while (GetMessage(&msg, NULL, 0, 0)) {
        if (msg.message == WM_HOTKEY) {
            HandleHotkey((int)msg.wParam);
        }
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    if (consoleEnabled)
        std::cout << "Exiting..." << std::endl;

    // Wait for monitor thread
    g_pcsx2Running = false;
    if (monitorThread.joinable())
        monitorThread.join();

    // Cleanup
    UnregisterHotkeys();
    CleanupPCSX2();
    
    if (consoleEnabled)
        FreeConsole();

    return (int)msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
        case WM_SIZE: {
            if (g_pcsx2Window) {
                RECT rect;
                GetClientRect(hwnd, &rect);
                SetWindowPos(g_pcsx2Window, NULL, 0, 0, rect.right, rect.bottom, SWP_NOZORDER);
            }
            break;
        }
        case WM_CLOSE: {
            if (consoleEnabled)
                std::cout << "Window closing..." << std::endl;
            
            // Terminate PCSX2 before closing
            TerminatePCSX2();
            
            DestroyWindow(hwnd);
            break;
        }
        case WM_DESTROY: {
            g_pcsx2Running = false;
            PostQuitMessage(0);
            break;
        }
        default:
            return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    return 0;
}

void RegisterHotkeys()
{
    RegisterHotKey(NULL, HOTKEY_UPDATE_MAPS, MOD_NOREPEAT, VK_F11);
    RegisterHotKey(NULL, HOTKEY_OPEN_FIRSTRUN, MOD_NOREPEAT | MOD_CONTROL, VK_F11);

    if (consoleEnabled)
        std::cout << "Global hotkeys registered." << std::endl;
}

void UnregisterHotkeys()
{
    UnregisterHotKey(NULL, HOTKEY_UPDATE_MAPS);
    UnregisterHotKey(NULL, HOTKEY_OPEN_FIRSTRUN);
}

void HandleHotkey(int hotkeyId)
{
    switch (hotkeyId) {
        case HOTKEY_UPDATE_MAPS: {
            if (consoleEnabled)
                std::cout << "Manual map update triggered..." << std::endl;
            
            // Get ISO path and region from config
            std::wstring isoPath = LoadConfigValue(L"ISO");
            std::wstring mapRegion = LoadConfigValue(L"Region");
            
            if (!isoPath.empty() && !mapRegion.empty()) {
                std::string isoPathNarrow(isoPath.begin(), isoPath.end());
                std::string mapRegionNarrow(mapRegion.begin(), mapRegion.end());
                
                UpdateMaps(isoPathNarrow, mapRegionNarrow, mainWindow);
            }
            break;
        }
        case HOTKEY_OPEN_FIRSTRUN: {
            if (consoleEnabled)
                std::cout << "Opening First Run configuration..." << std::endl;

            ShowFirstRunDialog(GetModuleHandle(NULL), mainWindow, true);
            
            if (consoleEnabled) {
                if (settings.cancelled)
                    std::cout << "Configuration cancelled." << std::endl;
                else
                    std::cout << "Configuration saved." << std::endl;
            }

            // Check if relaunch was requested
            if (!settings.cancelled && settings.relaunch) {
                if (consoleEnabled)
                    std::cout << "Relaunching launcher..." << std::endl;

                // Get path to current executable
                wchar_t exePath[MAX_PATH];
                GetModuleFileNameW(NULL, exePath, MAX_PATH);

                // Launch new instance
                STARTUPINFOW si = { sizeof(si) };
                PROCESS_INFORMATION pi = {0};
                if (!CreateProcessW(exePath, NULL, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
                    if (consoleEnabled)
                        std::cout << "Failed to relaunch launcher. Error: " << GetLastError() << std::endl;
                } else {
                    if (consoleEnabled)
                        std::cout << "Launcher restarted successfully!" << std::endl;
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }

                // Terminate PCSX2 and current launcher
                TerminatePCSX2();
                ExitProcess(0);
            }

            break;
        }
    }
}

BOOL WINAPI ConsoleHandler(DWORD signal)
{
    if (signal == CTRL_CLOSE_EVENT || signal == CTRL_C_EVENT || 
        signal == CTRL_BREAK_EVENT || signal == CTRL_LOGOFF_EVENT || 
        signal == CTRL_SHUTDOWN_EVENT)
    {
        // Any console event - terminate PCSX2 and exit
        if (consoleEnabled)
            std::cout << "Console closing - shutting down..." << std::endl;
        
        TerminatePCSX2();
        g_pcsx2Running = false;
        
        // Give a moment for cleanup
        Sleep(500);
        
        return FALSE; // Let Windows terminate us
    }
    return FALSE;
}
