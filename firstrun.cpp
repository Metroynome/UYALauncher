#pragma once
#include <windows.h>
#include <commdlg.h>
#include <commctrl.h>
#include <string>
#include "resource.h"  // Add at the very top of firstrun.cpp

#pragma comment(lib, "comctl32.lib")

// Control IDs for first run dialog
#define IDC_ISO_PATH_EDIT       1001
#define IDC_ISO_BROWSE_BTN      1002
#define IDC_PCSX2_PATH_EDIT     1003
#define IDC_PCSX2_BROWSE_BTN    1004
#define IDC_REGION_COMBO        1005
#define IDC_EMBED_CHECK         1006
#define IDC_OK_BTN              1007

struct FirstRunConfig {
    std::wstring isoPath;
    std::wstring pcsx2Path;
    std::wstring mapRegion;
    bool embedWindow;
    bool cancelled;
};

static FirstRunConfig g_firstRunConfig;
static HWND g_firstRunDlg = NULL;

// Validate and enable/disable Launch button
void ValidateLaunchButton(HWND hwnd) {
    wchar_t isoPath[MAX_PATH];
    wchar_t pcsx2Path[MAX_PATH];
    
    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
    
    // Enable button only if both paths are filled
    bool enableButton = (wcslen(isoPath) > 0 && wcslen(pcsx2Path) > 0);
    EnableWindow(GetDlgItem(hwnd, IDC_OK_BTN), enableButton);
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

// Dialog procedure for first run setup
INT_PTR CALLBACK FirstRunDlgProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_INITDIALOG:
        {
            g_firstRunDlg = hwnd;
            
            // Center the dialog
            RECT rc;
            GetWindowRect(hwnd, &rc);
            int x = (GetSystemMetrics(SM_CXSCREEN) - (rc.right - rc.left)) / 2;
            int y = (GetSystemMetrics(SM_CYSCREEN) - (rc.bottom - rc.top)) / 2;
            SetWindowPos(hwnd, NULL, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            
            // Check embed window by default
            CheckDlgButton(hwnd, IDC_EMBED_CHECK, BST_CHECKED);
            
            // Disable Launch button until both paths are filled
            EnableWindow(GetDlgItem(hwnd, IDC_OK_BTN), FALSE);
            
            return TRUE;
        }
        
        case WM_COMMAND:
        {
            // Check for text change in edit boxes
            if (HIWORD(wParam) == EN_CHANGE) {
                if (LOWORD(wParam) == IDC_ISO_PATH_EDIT || LOWORD(wParam) == IDC_PCSX2_PATH_EDIT) {
                    ValidateLaunchButton(hwnd);
                    return TRUE;
                }
            }
            
            switch (LOWORD(wParam)) {
                case IDC_ISO_BROWSE_BTN:
                {
                    std::wstring path = BrowseForISO(hwnd);
                    if (!path.empty())
                        SetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, path.c_str());
                    return TRUE;
                }
                
                case IDC_PCSX2_BROWSE_BTN:
                {
                    std::wstring path = BrowseForPCSX2(hwnd);
                    if (!path.empty())
                        SetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, path.c_str());
                    return TRUE;
                }
                
                case IDC_OK_BTN:
                {
                    // Get ISO path
                    wchar_t isoPath[MAX_PATH];
                    GetDlgItemTextW(hwnd, IDC_ISO_PATH_EDIT, isoPath, MAX_PATH);
                    g_firstRunConfig.isoPath = isoPath;
                    
                    // Get PCSX2 path
                    wchar_t pcsx2Path[MAX_PATH];
                    GetDlgItemTextW(hwnd, IDC_PCSX2_PATH_EDIT, pcsx2Path, MAX_PATH);
                    g_firstRunConfig.pcsx2Path = pcsx2Path;
                    
                    // Validate paths
                    if (g_firstRunConfig.isoPath.empty() || g_firstRunConfig.pcsx2Path.empty()) {
                        MessageBoxW(hwnd, L"Please select both ISO and PCSX2 paths.", L"Error", MB_OK | MB_ICONERROR);
                        return TRUE;
                    }
                    
                    // Get region selection
                    HWND hCombo = GetDlgItem(hwnd, IDC_REGION_COMBO);
                    int sel = SendMessageW(hCombo, CB_GETCURSEL, 0, 0);
                    wchar_t region[32];
                    SendMessageW(hCombo, CB_GETLBTEXT, sel, (LPARAM)region);
                    g_firstRunConfig.mapRegion = region;
                    
                    // Get embed checkbox
                    g_firstRunConfig.embedWindow = (IsDlgButtonChecked(hwnd, IDC_EMBED_CHECK) == BST_CHECKED);
                    
                    g_firstRunConfig.cancelled = false;
                    
                    // Destroy the window to exit the message loop
                    DestroyWindow(hwnd);
                    return TRUE;
                }
                
                case IDCANCEL:
                    g_firstRunConfig.cancelled = true;
                    DestroyWindow(hwnd);
                    return TRUE;
            }
            break;
        }
        
        case WM_CLOSE:
            g_firstRunConfig.cancelled = true;
            DestroyWindow(hwnd);
            return TRUE;
    }
    
    return FALSE;
}

// Show first run setup dialog
bool ShowFirstRunDialog(HINSTANCE hInstance, HWND parent) {
    WNDCLASSEXW wc = {0};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = DefDlgProcW;
    wc.hInstance = hInstance;
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = L"FirstRunDialogClass";
    wc.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));      // Add this line
    wc.hIconSm = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_MAINICON));    // Add this line
    RegisterClassExW(&wc);
    
    // Create controls with cleaner layout
    int windowWidth = 400;
    int windowHeight = 250;
    int y = 20;
    int labelWidth = 130;
    int editX = labelWidth + 10;
    int editWidth = 150;
    int browseX = editX + editWidth + 10;
    int browseWidth = 70;
    int comboBoxWidth = 70;
    int launchButtonWidth = 300;
    int launchButtonHeight = 40;
    int launchButtonX = (windowWidth - launchButtonWidth) / 2;

    // Create the dialog window - resizable
    HWND hwnd = CreateWindowExW(
        0,
        L"#32770", // Dialog class
        L"UYA Launcher - Setup",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_VISIBLE,
        CW_USEDEFAULT, CW_USEDEFAULT, windowWidth, windowHeight,
        parent, NULL, hInstance, NULL
    );
    
    if (!hwnd) return false;

    // ISO Path
    CreateWindowW(L"STATIC", L"UYA ISO File:", WS_CHILD | WS_VISIBLE | SS_LEFT,  10, y + 3, labelWidth, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL,  editX, y, editWidth, 24, hwnd, (HMENU)IDC_ISO_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, browseX, y, browseWidth, 24, hwnd, (HMENU)IDC_ISO_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    // PCSX2 Path
    CreateWindowW(L"STATIC", L"PCSX2 Executable:", WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP, 10, y + 3, labelWidth, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"EDIT", L"", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL, editX, y, editWidth, 24, hwnd, (HMENU)IDC_PCSX2_PATH_EDIT, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Browse", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, browseX, y, browseWidth, 24, hwnd, (HMENU)IDC_PCSX2_BROWSE_BTN, hInstance, NULL);
    y += 35;
    
    // Region
    CreateWindowW(L"STATIC", L"Region:", WS_CHILD | WS_VISIBLE | SS_LEFT,  10, y + 3, labelWidth, 20, hwnd, NULL, hInstance, NULL);
    HWND hCombo = CreateWindowW(L"COMBOBOX", L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL, editX, y, comboBoxWidth, 100, hwnd, (HMENU)IDC_REGION_COMBO, hInstance, NULL);
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"NTSC");
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"PAL");
    SendMessageW(hCombo, CB_ADDSTRING, 0, (LPARAM)L"Both");
    SendMessageW(hCombo, CB_SETCURSEL, 0, 0);
    y += 35;
    
    // Embed window checkbox
    CreateWindowW(L"STATIC", L"Embed Window:", WS_CHILD | WS_VISIBLE | SS_LEFT, 10, y + 3, labelWidth, 20, hwnd, NULL, hInstance, NULL);
    CreateWindowW(L"BUTTON", L"Embed PCSX2 in launcher", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, editX, y, 300, 20, hwnd, (HMENU)IDC_EMBED_CHECK, hInstance, NULL);
    CheckDlgButton(hwnd, IDC_EMBED_CHECK, BST_CHECKED);
    y += 35;
    
    // Launch button
    CreateWindowW(L"BUTTON", L"Launch", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, launchButtonX, y, launchButtonWidth, launchButtonHeight, hwnd, (HMENU)IDC_OK_BTN, hInstance, NULL);
    
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
    
    return !g_firstRunConfig.cancelled;
}
