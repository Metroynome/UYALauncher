#include "firstrun.h"
#include "config.h"
#include "updater.h"
#include <windows.h>
#include <commdlg.h>
#include <commctrl.h>
#include <string>
#include "resource.h"

#pragma comment(lib, "comctl32.lib")

#define IDC_ISO_PATH_EDIT           1001
#define IDC_ISO_BROWSE_BTN          1002
#define IDC_PCSX2_PATH_EDIT         1003
#define IDC_PCSX2_BROWSE_BTN        1004
#define IDC_REGION_COMBO            1005
#define IDC_EMBED_CHECK             1006
#define IDC_BOOT_MP_CHECK           1007
#define IDC_WIDESCREEN_CHECK        1008
#define IDC_PROGRESSIVE_SCAN_CHECK  1009
#define IDC_LAUNCH_BTN              1010
#define IDC_SAVE_BTN                1011
#define IDC_SAVE_RELAUNCH_BTN       1012
#define IDC_AUTO_UPDATE_CHECK       1013
#define IDC_UPDATE_BTN              1014
#define IDC_FULLSCREEN_CHECK        1015

static HWND g_firstRunDlg = NULL;
SettingsState settings;

void ValidateLaunchButton(HWND hwnd) {
    wchar_t isoPath[MAX_PATH];
    wchar_t pcsx2Path[MAX_PATH];
    
    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
    
    // Enable button only if both paths are filled
    bool enableButton = (wcslen(isoPath) > 0 && wcslen(pcsx2Path) > 0);
    EnableWindow(GetDlgItem(hwnd, IDC_LAUNCH_BTN), enableButton);
}

// Browse for ISO file
std::wstring BrowseForISO(HWND parent) {
    OPENFILENAMEW ofn = {0};
    wchar_t szFile[MAX_PATH] = {0};

    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = parent;
    ofn.lpstrFile = szFile;
    ofn.nMaxFile = MAX_PATH;
    ofn.lpstrFilter = L"PS2 ISO Files\0*.iso;*.bin;*.img\0All Files\0*.*\0";
    ofn.nFilterIndex = 1;
    ofn.lpstrTitle = L"Select UYA ISO File";
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

    if (GetOpenFileNameW(&ofn))
        return std::wstring(szFile);
    
    return L"";
}

// Browse for PCSX2 executable
std::wstring BrowseForPCSX2(HWND parent) {
    OPENFILENAMEW ofn = {0};
    wchar_t szFile[MAX_PATH] = {0};

    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = parent;
    ofn.lpstrFile = szFile;
    ofn.nMaxFile = MAX_PATH;
    ofn.lpstrFilter = L"PCSX2 Executable\0pcsx2.exe;pcsx2-qt.exe\0All Executables\0*.exe\0";
    ofn.nFilterIndex = 1;
    ofn.lpstrTitle = L"Select PCSX2 Executable";
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

    if (GetOpenFileNameW(&ofn))
        return std::wstring(szFile);
    
    return L"";
}

void GetConfigFromDialog(HWND hwnd, Configuration& config)
{
    // Get ISO path
    wchar_t isoPath[MAX_PATH];
    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
    config.isoPath = isoPath;

    // Get PCSX2 path
    wchar_t pcsx2Path[MAX_PATH];
    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
    config.pcsx2Path = pcsx2Path;

    // Validate paths
    if (config.isoPath.empty() || config.pcsx2Path.empty()) {
        MessageBoxW(hwnd, L"Please select both ISO and PCSX2 paths.", L"Error", MB_OK | MB_ICONERROR);
        return;
    }

    // Get region selection
    HWND hCombo = GetDlgItem(hwnd, IDC_REGION_COMBO);
    LRESULT sel = SendMessageW(hCombo, CB_GETCURSEL, 0, 0);
    wchar_t region[32];
    SendMessageW(hCombo, CB_GETLBTEXT, sel, (LPARAM)region);
    config.region = region;

    // get all other items
    config.autoUpdate = (IsDlgButtonChecked(hwnd, IDC_AUTO_UPDATE_CHECK) == BST_CHECKED);
    config.embedWindow = (IsDlgButtonChecked(hwnd, IDC_EMBED_CHECK) == BST_CHECKED);
    config.fullscreen = (IsDlgButtonChecked(hwnd, IDC_FULLSCREEN_CHECK) == BST_CHECKED);
    config.patches.bootToMultiplayer = (IsDlgButtonChecked(hwnd, IDC_BOOT_MP_CHECK) == BST_CHECKED);
    config.patches.widescreen = (IsDlgButtonChecked(hwnd, IDC_WIDESCREEN_CHECK) == BST_CHECKED);
    config.patches.progressiveScan = (IsDlgButtonChecked(hwnd, IDC_PROGRESSIVE_SCAN_CHECK) == BST_CHECKED);
}


INT_PTR CALLBACK FirstRunDlgProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_INITDIALOG: {
            g_firstRunDlg = hwnd;
            settings.relaunch = false;
            
            // Center the dialog
            RECT rc;
            GetWindowRect(hwnd, &rc);
            int x = (GetSystemMetrics(SM_CXSCREEN) - (rc.right - rc.left)) / 2;
            int y = (GetSystemMetrics(SM_CYSCREEN) - (rc.bottom - rc.top)) / 2;
            SetWindowPos(hwnd, NULL, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            
            // Validate launch button based on pre-populated values (if any)
            ValidateLaunchButton(hwnd);
            
            return TRUE;
        }
        
        case WM_COMMAND: {
            // Check for text change in edit boxes
            if (HIWORD(wParam) == EN_CHANGE) {
                if (LOWORD(wParam) == IDC_ISO_PATH_EDIT || LOWORD(wParam) == IDC_PCSX2_PATH_EDIT) {
                    ValidateLaunchButton(hwnd);
                    return TRUE;
                }
            }
            
            switch (LOWORD(wParam)) {
                case IDC_ISO_BROWSE_BTN: {
                    std::wstring path = BrowseForISO(hwnd);
                    if (!path.empty())
                        SetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, path.c_str());
                    return TRUE;
                }
                case IDC_PCSX2_BROWSE_BTN: {
                    std::wstring path = BrowseForPCSX2(hwnd);
                    if (!path.empty())
                        SetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, path.c_str());
                    return TRUE;
                }
                case IDC_LAUNCH_BTN: {
                    // Get Config from dialog settings
                    GetConfigFromDialog(hwnd, config);
                    
                    // Save to file
                    SaveConfig(config);
                    
                    // Apply
                    ApplyConfig(config);
                    
                    settings.cancelled = false;

                    // Destroy the window to exit the message loop
                    DestroyWindow(hwnd);
                    return true;
                }
                
                case IDCANCEL: {
                    settings.cancelled = true;
                    DestroyWindow(hwnd);
                    return true;
                }
                case IDC_SAVE_BTN:
                case IDC_SAVE_RELAUNCH_BTN: {
                    settings.relaunch = (LOWORD(wParam) == IDC_SAVE_RELAUNCH_BTN);

                    // Reuse existing OK logic
                    SendMessageW(hwnd, WM_COMMAND, IDC_LAUNCH_BTN, 0);
                    return true;
                }
                case IDC_UPDATE_BTN: {
                    EnableWindow(GetDlgItem(hwnd, IDC_UPDATE_BTN), FALSE);
                    SetDlgItemTextW(hwnd, IDC_UPDATE_BTN, L"Checking...");
                    
                    UpdateResult result = RunUpdater(false);  // false = not silent, show prompts
                    switch (result) {
                        case UpdateResult::UpToDate:
                            MessageBoxW(hwnd, L"You're on the latest version!", L"No Updates", MB_OK | MB_ICONINFORMATION);
                            break;
                        case UpdateResult::NetworkError:
                            MessageBoxW(hwnd, L"Cannot connect to update server. Check your internet connection.", L"Network Error", MB_OK | MB_ICONERROR);
                            break;
                        case UpdateResult::Failed:
                            MessageBoxW(hwnd, L"Update failed. Please try again later.", L"Error", MB_OK | MB_ICONERROR);
                            break;
                        case UpdateResult::UserCancelled:
                        case UpdateResult::Updated:
                            break;
                    }
                    EnableWindow(GetDlgItem(hwnd, IDC_UPDATE_BTN), TRUE);
                    SetDlgItemTextW(hwnd, IDC_UPDATE_BTN, L"Check for Updates");
                    return true;
                }
            }
            break;
        }
        
        case WM_CLOSE:
            settings.cancelled = true;
            DestroyWindow(hwnd);
            return true;
    }
    
    return false;
}

// Show first run setup dialog
bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent, bool hotkeyMode) {
    WNDCLASSEXW wc = {0};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = DefDlgProcW;
    wc.hInstance = hInstance;
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = L"FirstRunDialogClass";
    wc.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));
    wc.hIconSm = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));
    RegisterClassExW(&wc);
    
    // Create controls with cleaner layout
    int window_w = 405;
    int window_h = 395;
    int y = 20;
    int static_w = 135;
    int checkbox_x = static_w + 10;
    int combobox_x = static_w + 10;
    int edit_x = static_w + 10;
    int edit_w = 150;
    int browse_x = edit_x + edit_w + 10;
    int browse_w = 70;
    int combobox_w = 70;
    int button_w = 300;
    int button_h = 40;
    int button_x = (window_w - button_w) / 2;

    // increate height if hotkey for config is pressed.
    if (hotkeyMode) window_h += 90;

    // Create the dialog window - resizable
    HWND hwnd = CreateWindowExW(
        0,
        L"#32770",
        L"UYA Launcher - Setup",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_VISIBLE,
        CW_USEDEFAULT, CW_USEDEFAULT, window_w, window_h,
        parent, NULL, hInstance, NULL
    );
    
    if (!hwnd) return false;

    // ISO Path
    CreateWindowW(L"STATIC", L"UYA ISO File:", WS_CHILD | WS_VISIBLE | SS_LEFT,  10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL,  edit_x, y, edit_w, 24, hwnd, (HMENU)IDC_ISO_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, browse_x, y, browse_w, 24, hwnd, (HMENU)IDC_ISO_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    // PCSX2 Path
    CreateWindowW(L"STATIC", L"PCSX2 Executable:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL, edit_x, y, edit_w, 24, hwnd, (HMENU)IDC_PCSX2_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, browse_x, y, browse_w, 24, hwnd, (HMENU)IDC_PCSX2_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    // Region
    CreateWindowW(L"STATIC", L"Region:", WS_CHILD | WS_VISIBLE | SS_LEFT,  10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    HWND hCombo = CreateWindowW(L"COMBOBOX", L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL, combobox_x, y, combobox_w, 100, hwnd, (HMENU)IDC_REGION_COMBO, hInstance, NULL);
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"NTSC");
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"PAL");
    // SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"Both");
    SendMessageW(hCombo, CB_SETCURSEL, 0, 0);
    y += 35;

    // Auto Update Launcher checkbox
    CreateWindowW(L"STATIC", L"Auto Update:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_AUTO_UPDATE_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_AUTO_UPDATE_CHECK, BST_CHECKED);
    
    // Add version label next to checkbox
    std::wstring versionText = L"(v" + std::wstring(UYA_LAUNCHER_VERSION) + L")";
    CreateWindowW(L"STATIC", versionText.c_str(), WS_CHILD | WS_VISIBLE | SS_LEFT, checkbox_x + 25, y + 3, 100, 20, hwnd, NULL, hInstance, NULL);
    y += 35;

    // Enable Fullscreen
    CreateWindowW(L"STATIC", L"Start Fullscreen:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_FULLSCREEN_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_FULLSCREEN_CHECK, BST_CHECKED);
    y += 35;

    // Boot to Multiplayer checkbox
    CreateWindowW(L"STATIC", L"Boot to Multiplayer:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_BOOT_MP_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_BOOT_MP_CHECK, BST_CHECKED);
    y += 35;
    
    // WideScreen checkbox
    CreateWindowW(L"STATIC", L"Enable Widescreen:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_WIDESCREEN_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_WIDESCREEN_CHECK, BST_CHECKED);
    y += 35;

    // Progressive Scan checkbox
    // CreateWindowW(L"STATIC", L"Progressive Scan:", WS_CHILD | WS_VISIBLE | SS_LEFT, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    // CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_PROGRESSIVE_SCAN_CHECK, hInstance, NULL);
    // CheckDlgButton(hwnd, IDC_PROGRESSIVE_SCAN_CHECK, BST_CHECKED);
    // y += 35;

    // Embed window checkbox
    CreateWindowW(L"STATIC", L"Embed Window:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, static_w, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Embed PCSX2 in launcher", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_EMBED_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_EMBED_CHECK, BST_CHECKED);
    y += 35;
    
    // Launch button
    if (!hotkeyMode) {
        // Normal first run: Launch button
        CreateWindowW(L"BUTTON",L"Launch", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, button_x, y, button_w, button_h, hwnd, (HMENU)IDC_LAUNCH_BTN, hInstance, NULL);
    } else {
        // Check for Updates
        CreateWindowW(L"BUTTON", L"Check for Updates", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_UPDATE_BTN, hInstance, NULL);
        y += 40;

        // Hotkey mode: Save + Save & Relaunch
        CreateWindowW(L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_SAVE_BTN, hInstance, NULL);
        y += 40;

        CreateWindowW(L"BUTTON", L"Save and Relaunch", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_SAVE_RELAUNCH_BTN, hInstance, NULL);
    }
    
    // Load existing config and populate dialog
    Configuration existingConfig = LoadConfig();

    // Set ISO path if exists
    if (!existingConfig.isoPath.empty())
        SetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, existingConfig.isoPath.c_str());

    // Set PCSX2 path if exists
    if (!existingConfig.pcsx2Path.empty())
        SetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, existingConfig.pcsx2Path.c_str());

    // Set region dropdown if exists
    if (!existingConfig.region.empty()) {
        HWND hComboForInit = GetDlgItem(hwnd, IDC_REGION_COMBO);
        int option = (existingConfig.region == L"PAL") ? 1 : 
                    (existingConfig.region == L"Both") ? 2 : 0;
        SendMessageW(hComboForInit, CB_SETCURSEL, option, 0);
    }

    // Set all checkboxes from config
    CheckDlgButton(hwnd, IDC_EMBED_CHECK, existingConfig.embedWindow ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_FULLSCREEN_CHECK, existingConfig.fullscreen ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_BOOT_MP_CHECK, existingConfig.patches.bootToMultiplayer ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_WIDESCREEN_CHECK, existingConfig.patches.widescreen ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_PROGRESSIVE_SCAN_CHECK, existingConfig.patches.progressiveScan ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_AUTO_UPDATE_CHECK, existingConfig.autoUpdate ? BST_CHECKED : BST_UNCHECKED);

    // Set dialog proc
    SetWindowLongPtrW(hwnd, DWLP_DLGPROC, (LONG_PTR)FirstRunDlgProc);
    
    // Initialize dialog
    SendMessageW(hwnd, WM_INITDIALOG, 0, 0);
    
    // Message loop
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        if (!IsDialogMessageW(hwnd, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        
        if (!IsWindow(hwnd))
            break;
    }
    
    return !settings.cancelled;
}
