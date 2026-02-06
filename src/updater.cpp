#include "updater.h"
#include "config.h"
#include <windows.h>
#include <winhttp.h>
#include <urlmon.h>
#include <commctrl.h>
#include <string>
#include <vector>
#include <Shlwapi.h>
#pragma comment(lib, "Shlwapi.lib")
#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "comctl32.lib")

// Progress tracking
static HWND g_updateProgressWindow = NULL;
static HWND g_updateProgressBar = NULL;
static HWND g_updateStatusText = NULL;
static bool g_downloadComplete = false;

extern PROCESS_INFORMATION processInfo;

// Forward declarations for internal helpers
static std::wstring HttpGet(const std::wstring& host, const std::wstring& path);
static std::wstring ExtractJsonValue(const std::wstring& json, const std::wstring& key);
static bool IsNewerVersion(const std::wstring& current, const std::wstring& remote);
static LRESULT CALLBACK UpdateProgressWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

// Check for internet connectivity
static bool CheckForInternet()
{
    HINTERNET hSession = WinHttpOpen(
        L"UYALauncher/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME,
        WINHTTP_NO_PROXY_BYPASS,
        0);
    
    if (!hSession)
        return false;
    
    WinHttpCloseHandle(hSession);
    return true;
}

// Check if updates are available
static bool UpdatesAvailable(std::wstring& outUrl, std::wstring& outVersion)
{
    return CheckForUpdate(outUrl, outVersion);
}

// Progress window procedure
static LRESULT CALLBACK UpdateProgressWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg) {
        case WM_CLOSE:
            return 0; // Don't allow closing during download
        default:
            return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    return 0;
}

// Download progress callback class
class DownloadBindStatusCallback : public IBindStatusCallback
{
public:
    STDMETHOD(OnStartBinding)(DWORD, IBinding*) { return S_OK; }
    STDMETHOD(GetPriority)(LONG*) { return E_NOTIMPL; }
    STDMETHOD(OnLowResource)(DWORD) { return S_OK; }
    STDMETHOD(OnProgress)(ULONG ulProgress, ULONG ulProgressMax, ULONG ulStatusCode, LPCWSTR szStatusText)
    {
        if (ulProgressMax > 0 && g_updateProgressBar) {
            int percent = (int)((ulProgress * 100) / ulProgressMax);
            SendMessage(g_updateProgressBar, PBM_SETPOS, percent, 0);
            
            wchar_t status[256];
            swprintf_s(status, L"Downloading update... %d%%", percent);
            SendMessage(g_updateStatusText, WM_SETTEXT, 0, (LPARAM)status);
        }
        return S_OK;
    }
    STDMETHOD(OnStopBinding)(HRESULT, LPCWSTR) { g_downloadComplete = true; return S_OK; }
    STDMETHOD(GetBindInfo)(DWORD*, BINDINFO*) { return S_OK; }
    STDMETHOD(OnDataAvailable)(DWORD, DWORD, FORMATETC*, STGMEDIUM*) { return S_OK; }
    STDMETHOD(OnObjectAvailable)(REFIID, IUnknown*) { return S_OK; }
    STDMETHOD_(ULONG, AddRef)() { return 1; }
    STDMETHOD_(ULONG, Release)() { return 1; }
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv)
    {
        if (riid == IID_IUnknown || riid == IID_IBindStatusCallback) {
            *ppv = this;
            return S_OK;
        }
        return E_NOINTERFACE;
    }
};

// Download the update
static bool DownloadUpdates(const std::wstring& downloadUrl, const std::wstring& outPath, bool silent)
{
    // Always show progress window
    WNDCLASSEXW wc = {0};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = UpdateProgressWndProc;
    wc.hInstance = GetModuleHandle(NULL);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = L"UpdateProgressClass";
    RegisterClassExW(&wc);

    g_updateProgressWindow = CreateWindowExW(
        0,
        L"UpdateProgressClass",
        L"Downloading Update",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU,
        CW_USEDEFAULT, CW_USEDEFAULT, 400, 150,
        NULL, NULL, GetModuleHandle(NULL), NULL
    );

    if (!g_updateProgressWindow) {
        return DownloadFile(downloadUrl, outPath);
    }

    // Create status text
    g_updateStatusText = CreateWindowW(
        L"STATIC", L"Starting download...",
        WS_CHILD | WS_VISIBLE | SS_CENTER,
        10, 20, 370, 30,
        g_updateProgressWindow, NULL, GetModuleHandle(NULL), NULL
    );

    // Create progress bar
    g_updateProgressBar = CreateWindowW(
         PROGRESS_CLASSW, NULL,
        WS_CHILD | WS_VISIBLE,
        10, 60, 370, 30,
        g_updateProgressWindow, NULL, GetModuleHandle(NULL), NULL
    );

    SendMessage(g_updateProgressBar, PBM_SETRANGE, 0, MAKELPARAM(0, 100));

    ShowWindow(g_updateProgressWindow, SW_SHOW);
    UpdateWindow(g_updateProgressWindow);

    g_downloadComplete = false;

    // Download with progress callback
    DownloadBindStatusCallback callback;
    HRESULT hr = URLDownloadToFileW(NULL, downloadUrl.c_str(), outPath.c_str(), 0, &callback);
    
    // Show completion
    if (SUCCEEDED(hr)) {
        SendMessage(g_updateStatusText, WM_SETTEXT, 0, (LPARAM)L"Download complete!");
        Sleep(500);
    }

    DestroyWindow(g_updateProgressWindow);
    UnregisterClassW(L"UpdateProgressClass", GetModuleHandle(NULL));
    g_updateProgressWindow = NULL;
    g_updateProgressBar = NULL;
    g_updateStatusText = NULL;

    return SUCCEEDED(hr);
}

// Apply the downloaded update
static bool ApplyUpdates(const std::wstring& newExePath, bool silent)
{
    wchar_t currentExe[MAX_PATH];
    GetModuleFileNameW(NULL, currentExe, MAX_PATH);
    
    std::wstring cmdLine = L"\"" + std::wstring(currentExe) + L"\" --self-update \"" + newExePath + L"\"";
    
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = {0};
    
    if (!CreateProcessW(NULL, &cmdLine[0], NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi))
        return false;
    
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    
    return true;
}

// Main updater function
UpdateResult RunUpdater(bool silent)
{
    // Check internet connectivity
    if (!CheckForInternet())
        return UpdateResult::NetworkError;

    std::wstring downloadUrl;
    std::wstring remoteVersion;

    // Check if update is available
    if (!UpdatesAvailable(downloadUrl, remoteVersion))
        return UpdateResult::UpToDate;

    // Ask user only if not silent
    if (!silent)
    {
        std::wstring message = L"Update available: v" + remoteVersion + L"\n\n"
                              L"Current version: v" UYA_LAUNCHER_VERSION L"\n"
                              L"New version: v" + remoteVersion + L"\n\n"
                              L"Download and install now?\n\n"
                              L"Note: This will close the launcher and PCSX2.";

        if (MessageBoxW(NULL, message.c_str(), L"Update Available", MB_YESNO | MB_ICONQUESTION) != IDYES)
            return UpdateResult::UserCancelled;
    }

    // Prepare temp path for new exe
    wchar_t tempPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tempPath);
    std::wstring newExePath = std::wstring(tempPath) + L"UYALauncher_new.exe";

    // Download update
    if (!DownloadUpdates(downloadUrl, newExePath, silent))
        return UpdateResult::Failed;

    // *** ADD THIS SECTION HERE ***
    // Terminate PCSX2 if it's running (before exiting)
    if (processInfo.hProcess) {
        TerminateProcess(processInfo.hProcess, 0);
        WaitForSingleObject(processInfo.hProcess, 2000);
        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);
    }
    // *** END OF NEW SECTION ***

    // Get current exe path
    wchar_t currentExe[MAX_PATH];
    GetModuleFileNameW(NULL, currentExe, MAX_PATH);

    // Build command line with pipe separator: --self-update "<newExePath>|<version>"
    std::wstring cmdLine = L"\"" + std::wstring(currentExe) + L"\" --self-update \"" + 
                           newExePath + L"|" + remoteVersion + L"\"";

    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = {0};

    if (!CreateProcessW(NULL, &cmdLine[0], NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        return UpdateResult::Failed;
    }

    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    // Exit immediately
    ExitProcess(0);

    return UpdateResult::Updated;
}

// Self-update function
void RunSelfUpdate(const std::wstring& newExePath, const std::wstring& version)
{
    // Record the installed version before replacing
    SetInstalledVersion(version);

    wchar_t currentExe[MAX_PATH];
    GetModuleFileNameW(NULL, currentExe, MAX_PATH);

    // Wait a bit to ensure the original exe is ready to be replaced
    Sleep(1000);

    // Replace the old executable with the new one
    MoveFileExW(newExePath.c_str(), currentExe, MOVEFILE_REPLACE_EXISTING);

    // Launch the updated executable with --just-updated flag
    std::wstring cmdLine = L"\"" + std::wstring(currentExe) + L"\" --just-updated";
    ShellExecuteW(NULL, L"open", currentExe, cmdLine.c_str(), NULL, SW_SHOW);

    // Exit the old process
    ExitProcess(0);
}



std::wstring GetExecutablePath()
{
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    return path;
}

static std::wstring HttpGet(const std::wstring& host, const std::wstring& path)
{
    HINTERNET hSession = WinHttpOpen(
        L"UYALauncher/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME,
        WINHTTP_NO_PROXY_BYPASS,
        0);

    if (!hSession)
        return L"";

    HINTERNET hConnect = WinHttpConnect(hSession, host.c_str(), INTERNET_DEFAULT_HTTPS_PORT, 0);
    if (!hConnect)
    {
        WinHttpCloseHandle(hSession);
        return L"";
    }

    HINTERNET hRequest = WinHttpOpenRequest(
        hConnect,
        L"GET",
        path.c_str(),
        NULL,
        WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES,
        WINHTTP_FLAG_SECURE);

    if (!hRequest)
    {
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        return L"";
    }

    WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0, WINHTTP_NO_REQUEST_DATA, 0, 0, 0);
    WinHttpReceiveResponse(hRequest, NULL);

    // Read as bytes (char), not wide chars
    std::string response;
    DWORD bytesRead = 0;
    char buffer[1024];

    while (WinHttpReadData(hRequest, buffer, sizeof(buffer), &bytesRead) && bytesRead)
    {
        response.append(buffer, bytesRead);
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);

    // Convert narrow string to wide string
    if (response.empty())
        return L"";
    
    int size = MultiByteToWideChar(CP_UTF8, 0, response.c_str(), -1, NULL, 0);
    std::wstring wresponse(size, 0);
    MultiByteToWideChar(CP_UTF8, 0, response.c_str(), -1, &wresponse[0], size);
    
    return wresponse;
}

static std::wstring ExtractJsonValue(const std::wstring& json, const std::wstring& key)
{
    std::wstring needle = L"\"" + key + L"\":\"";
    size_t start = json.find(needle);
    if (start == std::wstring::npos)
        return L"";

    start += needle.length();
    size_t end = json.find(L"\"", start);
    if (end == std::wstring::npos)
        return L"";

    return json.substr(start, end - start);
}

// Helper function to compare versions
static bool IsNewerVersion(const std::wstring& current, const std::wstring& remote)
{
    auto trim = [](const std::wstring& str) -> std::wstring {
        size_t start = str.find_first_not_of(L" \t\r\n");
        size_t end = str.find_last_not_of(L" \t\r\n");
        return (start == std::wstring::npos) ? L"" : str.substr(start, end - start + 1);
    };

    auto split = [&](const std::wstring& version) -> std::vector<int> {
        std::vector<int> parts;
        std::wstring temp;
        std::wstring v = trim(version);

        for (wchar_t c : v) {
            if (c == L'.') {
                if (!temp.empty()) {
                    parts.push_back(std::stoi(temp));
                    temp.clear();
                }
            } else if (iswdigit(c)) {
                temp += c;
            }
        }
        if (!temp.empty()) {
            parts.push_back(std::stoi(temp));
        }

        while (parts.size() < 3) parts.push_back(0); // pad to major.minor.patch
        return parts;
    };

    auto cur = split(current);
    auto rem = split(remote);

    for (size_t i = 0; i < 3; i++) {
        if (rem[i] > cur[i]) return true;
        if (rem[i] < cur[i]) return false;
    }

    return false; // equal
}

bool CheckForUpdate(std::wstring& outDownloadUrl, std::wstring& outRemoteVersion)
{
    std::wstring json = HttpGet(L"api.github.com", L"/repos/" GitUser L"/" GitRepo L"/releases/latest");

    if (json.empty())
        return false;

    std::wstring tag = ExtractJsonValue(json, L"tag_name");
    if (tag.empty())
        return false;

    if (tag[0] == L'v')
        tag = tag.substr(1);

    outRemoteVersion = tag;

    std::wstring currentVersion = GetInstalledVersion();
    if (!IsNewerVersion(currentVersion, tag))
        return false;

    outDownloadUrl = ExtractJsonValue(json, L"browser_download_url");

    return !outDownloadUrl.empty();
}

bool DownloadFile(const std::wstring& url, const std::wstring& outPath)
{
    return SUCCEEDED(URLDownloadToFileW(NULL, url.c_str(), outPath.c_str(), 0, NULL));
}
