#include "updater.h"
#include <windows.h>
#include <winhttp.h>
#include <string>
#include <urlmon.h>
#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "winhttp.lib")

bool CheckForUpdate(std::wstring& outUrl, std::wstring& outRemoteVersion)
{
    outUrl.clear();
    outRemoteVersion.clear();
    return false;
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

bool CheckForLauncherUpdate(std::wstring& outDownloadUrl,
                            std::wstring& outRemoteVersion)
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

    // Find first asset download URL
    size_t assetPos = json.find(L"browser_download_url");
    if (assetPos == std::wstring::npos)
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
    Sleep(1000); // let old process exit
    MoveFileExW(newExe.c_str(), currentExe, MOVEFILE_REPLACE_EXISTING);
    ShellExecuteW(NULL, L"open", currentExe, NULL, NULL, SW_SHOW);
}