#include "firstrun.h"
#include "config.h"
#include "updater.h"
#include <windows.h>
#include <commdlg.h>
#include <commctrl.h>
#include <string>
#include "resource.h"
#include "pcsx2.h"
#include <uxtheme.h>


#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "uxtheme.lib")

#define IDC_TAB_CONTROL             1020

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
#define IDC_CONSOLE_CHECK           1016

#define IDC_ISO_LABEL               2001
#define IDC_PCSX2_LABEL             2002
#define IDC_REGION_LABEL            2003
#define IDC_AUTO_UPDATE_LABEL       2004
#define IDC_FULLSCREEN_LABEL        2005
#define IDC_BOOT_MP_LABEL           2006
#define IDC_WIDESCREEN_LABEL        2007
#define IDC_PROGRESSIVE_SCAN_LABEL  2008
#define IDC_EMBED_LABEL             2009
#define IDC_CONSOLE_LABEL           2010
#define IDC_VERSION_LABEL           2011

HWND g_firstRunDlg = NULL;
HWND g_tabControl = NULL;
SettingsState settings;

enum TabPage {
    TAB_GENERAL = 0,
    TAB_PATCHES = 1,
    TAB_ADVANCED = 2
};

int currentTab = TAB_GENERAL;

void ShowTabControls(HWND hwnd, int tabIndex) {
    currentTab = tabIndex;
    
    int generalShow = (tabIndex == TAB_GENERAL) ? SW_SHOW : SW_HIDE;
    ShowWindow(GetDlgItem(hwnd, IDC_ISO_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_ISO_PATH_EDIT), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_ISO_BROWSE_BTN), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_PCSX2_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_PCSX2_PATH_EDIT), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_PCSX2_BROWSE_BTN), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_REGION_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_REGION_COMBO), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_AUTO_UPDATE_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_AUTO_UPDATE_CHECK), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_VERSION_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_FULLSCREEN_LABEL), generalShow);
    ShowWindow(GetDlgItem(hwnd, IDC_FULLSCREEN_CHECK), generalShow);
    
    int patchesShow = (tabIndex == TAB_PATCHES) ? SW_SHOW : SW_HIDE;
    ShowWindow(GetDlgItem(hwnd, IDC_BOOT_MP_LABEL), patchesShow);
    ShowWindow(GetDlgItem(hwnd, IDC_BOOT_MP_CHECK), patchesShow);
    ShowWindow(GetDlgItem(hwnd, IDC_WIDESCREEN_LABEL), patchesShow);
    ShowWindow(GetDlgItem(hwnd, IDC_WIDESCREEN_CHECK), patchesShow);
    ShowWindow(GetDlgItem(hwnd, IDC_PROGRESSIVE_SCAN_LABEL), patchesShow);
    ShowWindow(GetDlgItem(hwnd, IDC_PROGRESSIVE_SCAN_CHECK), patchesShow);
    
    int advancedShow = (tabIndex == TAB_ADVANCED) ? SW_SHOW : SW_HIDE;
    ShowWindow(GetDlgItem(hwnd, IDC_EMBED_LABEL), advancedShow);
    ShowWindow(GetDlgItem(hwnd, IDC_EMBED_CHECK), advancedShow);
    ShowWindow(GetDlgItem(hwnd, IDC_CONSOLE_LABEL), advancedShow);
    ShowWindow(GetDlgItem(hwnd, IDC_CONSOLE_CHECK), advancedShow);
}

void ValidateLaunchButton(HWND hwnd) {
    wchar_t isoPath[MAX_PATH];
    wchar_t pcsx2Path[MAX_PATH];
    
    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
    
    bool enableButton = (wcslen(isoPath) > 0 && wcslen(pcsx2Path) > 0);
    EnableWindow(GetDlgItem(hwnd, IDC_LAUNCH_BTN), enableButton);
}

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

void GetConfigFromDialog(HWND hwnd, Configuration& config) {
    wchar_t isoPath[MAX_PATH];
    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
    config.isoPath = isoPath;

    wchar_t pcsx2Path[MAX_PATH];
    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
    config.pcsx2Path = pcsx2Path;

    if (config.isoPath.empty() || config.pcsx2Path.empty()) {
        MessageBoxW(hwnd, L"Please select both ISO and PCSX2 paths.", L"Error", MB_OK | MB_ICONERROR);
        return;
    }

    HWND hCombo = GetDlgItem(hwnd, IDC_REGION_COMBO);
    LRESULT sel = SendMessageW(hCombo, CB_GETCURSEL, 0, 0);
    wchar_t region[32];
    SendMessageW(hCombo, CB_GETLBTEXT, sel, (LPARAM)region);
    config.region = region;

    config.autoUpdate = (IsDlgButtonChecked(hwnd, IDC_AUTO_UPDATE_CHECK) == BST_CHECKED);
    config.embedWindow = (IsDlgButtonChecked(hwnd, IDC_EMBED_CHECK) == BST_CHECKED);
    config.fullscreen = (IsDlgButtonChecked(hwnd, IDC_FULLSCREEN_CHECK) == BST_CHECKED);
    config.showConsole = (IsDlgButtonChecked(hwnd, IDC_CONSOLE_CHECK) == BST_CHECKED);
    config.patches.bootToMultiplayer = (IsDlgButtonChecked(hwnd, IDC_BOOT_MP_CHECK) == BST_CHECKED);
    config.patches.widescreen = (IsDlgButtonChecked(hwnd, IDC_WIDESCREEN_CHECK) == BST_CHECKED);
    config.patches.progressiveScan = (IsDlgButtonChecked(hwnd, IDC_PROGRESSIVE_SCAN_CHECK) == BST_CHECKED);
}

INT_PTR CALLBACK FirstRunDlgProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_INITDIALOG: {
            g_firstRunDlg = hwnd;
            settings.relaunch = false;
            
            RECT rc;
            GetWindowRect(hwnd, &rc);
            int x = (GetSystemMetrics(SM_CXSCREEN) - (rc.right - rc.left)) / 2;
            int y = (GetSystemMetrics(SM_CYSCREEN) - (rc.bottom - rc.top)) / 2;
            SetWindowPos(hwnd, NULL, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            ShowTabControls(hwnd, TAB_GENERAL);
            
            ValidateLaunchButton(hwnd);
            return TRUE;
        }
        
        case WM_NOTIFY: {
            NMHDR* pnmhdr = (NMHDR*)lParam;
            if (pnmhdr->idFrom == IDC_TAB_CONTROL && pnmhdr->code == TCN_SELCHANGE) {
                int tabIndex = TabCtrl_GetCurSel(g_tabControl);
                ShowTabControls(hwnd, tabIndex);
                return TRUE;
            }
            break;
        }
        
        case WM_COMMAND: {
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
                    GetConfigFromDialog(hwnd, config);
                    SaveConfig(config);
                    ApplyConfig(config);
                    settings.cancelled = false;
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
                    SendMessageW(hwnd, WM_COMMAND, IDC_LAUNCH_BTN, 0);
                    return true;
                }
                case IDC_UPDATE_BTN: {
                    EnableWindow(GetDlgItem(hwnd, IDC_UPDATE_BTN), FALSE);
                    SetWindowTextW(GetDlgItem(hwnd, IDC_UPDATE_BTN), L"Checking...");
                    RunUpdater(false);
                    SetWindowTextW(GetDlgItem(hwnd, IDC_UPDATE_BTN), L"Check for Updates");
                    EnableWindow(GetDlgItem(hwnd, IDC_UPDATE_BTN), TRUE);
                    return TRUE;
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

bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent, bool hotkeyMode)
{
    WNDCLASSEXW wc = {0};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = DefDlgProcW;
    wc.hInstance = hInstance;
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = L"FirstRunDialogClass";
    wc.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));
    wc.hIconSm = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));
    RegisterClassExW(&wc);

    int window_w = 450;
    int window_h = 420;
    int tab_margin = 10;
    int tab_h = 280;
    int y = 50;
    int static_w = 135;
    int checkbox_x = static_w + 10;
    int edit_x = static_w + 10;
    int edit_w = 180;
    int browse_x = edit_x + edit_w + 10;
    int browse_w = 70;
    int combobox_x = static_w + 10;
    int combobox_w = 70;
    int button_w = 300;
    int button_h = 40;
    int button_x = (window_w - button_w) / 2;

    if (hotkeyMode) window_h += 90;

    HWND hwnd = CreateWindowExW(
        WS_EX_TOPMOST,
        L"#32770",
        L"UYA Launcher - Settings",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_VISIBLE,
        CW_USEDEFAULT, CW_USEDEFAULT, window_w, window_h,
        NULL, NULL, hInstance, NULL
    );
    
    if (!hwnd) return false;

    g_tabControl = CreateWindowW(
        WC_TABCONTROLW,
        L"",
        WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
        tab_margin, tab_margin, 
        window_w - (tab_margin * 2) - 15,
        tab_h,
        hwnd,
        (HMENU)IDC_TAB_CONTROL,
        hInstance,
        NULL
    );
    SetWindowTheme(g_tabControl, L"", L"");

    TCITEMW tie = {0};
    tie.mask = TCIF_TEXT;
    
    tie.pszText = (LPWSTR)L"General";
    TabCtrl_InsertItem(g_tabControl, TAB_GENERAL, &tie);
    
    tie.pszText = (LPWSTR)L"Patches";
    TabCtrl_InsertItem(g_tabControl, TAB_PATCHES, &tie);
    
    tie.pszText = (LPWSTR)L"Advanced";
    TabCtrl_InsertItem(g_tabControl, TAB_ADVANCED, &tie);

    RECT rcTab;
    GetClientRect(g_tabControl, &rcTab);
    TabCtrl_AdjustRect(g_tabControl, FALSE, &rcTab);
    
    int tab_content_x = tab_margin + rcTab.left;
    int tab_content_y = tab_margin + rcTab.top;
    y = tab_content_y + 10;

    // GENERAL TAB CONTROLS
    CreateWindowW(L"STATIC", L"UYA ISO File:", WS_CHILD | SS_LEFT, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_ISO_LABEL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_BORDER | ES_AUTOHSCROLL, tab_content_x + edit_x, y, edit_w, 24, hwnd, (HMENU)IDC_ISO_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | BS_PUSHBUTTON, tab_content_x + browse_x, y, browse_w, 24, hwnd, (HMENU)IDC_ISO_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    CreateWindowW(L"STATIC", L"PCSX2 Executable:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_PCSX2_LABEL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_BORDER | ES_AUTOHSCROLL, tab_content_x + edit_x, y, edit_w, 24, hwnd, (HMENU)IDC_PCSX2_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | BS_PUSHBUTTON, tab_content_x + browse_x, y, browse_w, 24, hwnd, (HMENU)IDC_PCSX2_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    CreateWindowW(L"STATIC", L"Region:", WS_CHILD | SS_LEFT, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_REGION_LABEL, hInstance, NULL);
    HWND hCombo = CreateWindowW(L"COMBOBOX", L"", WS_CHILD | CBS_DROPDOWNLIST | WS_VSCROLL, tab_content_x + combobox_x, y, combobox_w, 100, hwnd, (HMENU)IDC_REGION_COMBO, hInstance, NULL);
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"NTSC");
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"PAL");
    SendMessageW(hCombo, CB_SETCURSEL, 0, 0);
    y += 35;

    CreateWindowW(L"STATIC", L"Auto Update:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_AUTO_UPDATE_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_AUTO_UPDATE_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_AUTO_UPDATE_CHECK, BST_CHECKED);
    std::wstring versionText = L"(v" + std::wstring(UYA_LAUNCHER_VERSION) + L")";
    CreateWindowW(L"STATIC", versionText.c_str(), WS_CHILD | SS_LEFT, tab_content_x + checkbox_x + 25, y + 3, 100, 20, hwnd, (HMENU)(INT_PTR)IDC_VERSION_LABEL, hInstance, NULL);
    y += 35;

    CreateWindowW(L"STATIC", L"Start Fullscreen:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_FULLSCREEN_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_FULLSCREEN_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_FULLSCREEN_CHECK, BST_CHECKED);

    // PATCHES TAB CONTROLS
    y = tab_content_y + 10;
    
    CreateWindowW(L"STATIC", L"Boot to Multiplayer:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_BOOT_MP_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_BOOT_MP_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_BOOT_MP_CHECK, BST_CHECKED);
    y += 35;
    
    CreateWindowW(L"STATIC", L"Enable Widescreen:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_WIDESCREEN_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_WIDESCREEN_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_WIDESCREEN_CHECK, BST_CHECKED);
    y += 35;

    CreateWindowW(L"STATIC", L"Progressive Scan:", WS_CHILD | SS_LEFT, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_PROGRESSIVE_SCAN_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_PROGRESSIVE_SCAN_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_PROGRESSIVE_SCAN_CHECK, BST_CHECKED);

    // ADVANCED TAB CONTROLS
    y = tab_content_y + 10;
    
    CreateWindowW(L"STATIC", L"Embed Window:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_EMBED_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Embed PCSX2 in launcher", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_EMBED_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_EMBED_CHECK, BST_CHECKED);
    y += 35;

    CreateWindowW(L"STATIC", L"Show Console:", WS_CHILD | SS_LEFTNOWORDWRAP, tab_content_x + 10, y + 3, static_w, 20, hwnd, (HMENU)(INT_PTR)IDC_CONSOLE_LABEL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Enable console for debugging", WS_CHILD | BS_AUTOCHECKBOX, tab_content_x + checkbox_x, y, 300, 20, hwnd, (HMENU)IDC_CONSOLE_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_CONSOLE_CHECK, BST_UNCHECKED);

    // BUTTONS
    y = tab_margin + tab_h + 15;
    
    if (!hotkeyMode) {
        CreateWindowW(L"BUTTON", L"Launch", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, button_x, y, button_w, button_h, hwnd, (HMENU)IDC_LAUNCH_BTN, hInstance, NULL);
    } else {
        CreateWindowW(L"BUTTON", L"Check for Updates", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_UPDATE_BTN, hInstance, NULL);
        y += 40;
        CreateWindowW(L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_SAVE_BTN, hInstance, NULL);
        y += 40;
        CreateWindowW(L"BUTTON", L"Save and Relaunch", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, button_x, y, button_w, 30, hwnd, (HMENU)IDC_SAVE_RELAUNCH_BTN, hInstance, NULL);
    }
    
    Configuration existingConfig;
    if (!IsFirstRun()) {
        existingConfig = LoadConfig();
    } else {
        existingConfig.region = L"NTSC";
        existingConfig.autoUpdate = true;
        existingConfig.embedWindow = true;
        existingConfig.fullscreen = true;
        existingConfig.patches.bootToMultiplayer = true;
        existingConfig.patches.widescreen = true;
        existingConfig.patches.progressiveScan = true;
        existingConfig.showConsole = false;
    }

    if (!existingConfig.isoPath.empty())
        SetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, existingConfig.isoPath.c_str());
    if (!existingConfig.pcsx2Path.empty())
        SetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, existingConfig.pcsx2Path.c_str());
    if (!existingConfig.region.empty()) {
        HWND hComboForInit = GetDlgItem(hwnd, IDC_REGION_COMBO);
        int option = (existingConfig.region == L"PAL") ? 1 : 0;
        SendMessageW(hComboForInit, CB_SETCURSEL, option, 0);
    }

    CheckDlgButton(hwnd, IDC_EMBED_CHECK, existingConfig.embedWindow ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_FULLSCREEN_CHECK, existingConfig.fullscreen ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_BOOT_MP_CHECK, existingConfig.patches.bootToMultiplayer ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_WIDESCREEN_CHECK, existingConfig.patches.widescreen ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_PROGRESSIVE_SCAN_CHECK, existingConfig.patches.progressiveScan ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_AUTO_UPDATE_CHECK, existingConfig.autoUpdate ? BST_CHECKED : BST_UNCHECKED);
    CheckDlgButton(hwnd, IDC_CONSOLE_CHECK, existingConfig.showConsole ? BST_CHECKED : BST_UNCHECKED);

    SetWindowLongPtrW(hwnd, DWLP_DLGPROC, (LONG_PTR)FirstRunDlgProc);
    SendMessageW(hwnd, WM_INITDIALOG, 0, 0);
    
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