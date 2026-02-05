#include "updater.h"
#include <windows.h>
#include <winhttp.h>
#include <urlmon.h>
#include <string>

#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "winhttp.lib")

// Forward declarations for internal helpers
static std::wstring HttpGet(const std::wstring& host, const std::wstring& path);
static std::wstring ExtractJsonValue(const std::wstring& json, const std::wstring& key);

// Check for internet connectivity
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

// Download the update
static bool DownloadUpdates(const std::wstring& downloadUrl, const std::wstring& outPath, bool silent)
{
    if (!silent) {
        // Could show progress dialog here if needed
    }
    
    return DownloadFile(downloadUrl, outPath);
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
    if (!CheckForInternet())
        return UpdateResult::NetworkError;
    
    std::wstring downloadUrl;
    std::wstring remoteVersion;
    
    if (!UpdatesAvailable(downloadUrl, remoteVersion))
        return UpdateResult::UpToDate;
    
    // Ask user if not silent
    if (!silent) {
        std::wstring message = L"Update available: v" + remoteVersion + L"\n\n"
                              L"Current version: v" UYA_LAUNCHER_VERSION L"\n"
                              L"New version: v" + remoteVersion + L"\n\n"
                              L"Download and install now?";
        
        if (MessageBoxW(NULL, message.c_str(), L"Update Available", MB_YESNO | MB_ICONQUESTION) != IDYES)
            return UpdateResult::UserCancelled;
    }
    // If silent mode, automatically proceed with update
    
    // Download to temp file
    wchar_t tempPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tempPath);
    std::wstring newExePath = std::wstring(tempPath) + L"UYALauncher_new.exe";
    
    if (!DownloadUpdates(downloadUrl, newExePath, silent))
        return UpdateResult::Failed;
    
    if (!ApplyUpdates(newExePath, silent))
        return UpdateResult::Failed;
    
    // Exit to let updater replace the file
    ExitProcess(0);
    
    return UpdateResult::Updated;
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

    std::wstring response;
    DWORD bytesRead = 0;
    wchar_t buffer[1024];

    while (WinHttpReadData(hRequest, buffer, sizeof(buffer), &bytesRead) && bytesRead)
    {
        response.append(buffer, bytesRead / sizeof(wchar_t));
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);

    return response;
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

    if (tag == UYA_LAUNCHER_VERSION)
        return false;

    outDownloadUrl = ExtractJsonValue(json, L"browser_download_url");

    return !outDownloadUrl.empty();
}

bool DownloadFile(const std::wstring& url, const std::wstring& outPath)
{
    return SUCCEEDED(URLDownloadToFileW(NULL, url.c_str(), outPath.c_str(), 0, NULL));
}

void RunSelfUpdate()
{
    std::wstring cmd = GetCommandLineW();
    size_t pos = cmd.find(L"--self-update");
    if (pos == std::wstring::npos)
        return;

    std::wstring newExe = cmd.substr(pos + 14);
    newExe.erase(0, newExe.find_first_not_of(L" \""));
    newExe.erase(newExe.find_last_not_of(L" \"") + 1);

    wchar_t currentExe[MAX_PATH];
    GetModuleFileNameW(NULL, currentExe, MAX_PATH);
    Sleep(1000);
    MoveFileExW(newExe.c_str(), currentExe, MOVEFILE_REPLACE_EXISTING);
    ShellExecuteW(NULL, L"open", currentExe, NULL, NULL, SW_SHOW);
}