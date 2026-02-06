#pragma once
#include <string>

#define UYA_LAUNCHER_VERSION L"1.0.0"

#define GitUser L"Metroynome"
#define GitRepo L"UYALauncher"

enum class UpdateResult {
    UpToDate,
    Updated,
    NetworkError,
    Failed,
    UserCancelled
};

UpdateResult RunUpdater(bool silent);
bool CheckForUpdate(std::wstring& outDownloadUrl, std::wstring& outRemoteVersion);
void RunSelfUpdate();
bool DownloadFile(const std::wstring& url, const std::wstring& outPath);
std::wstring GetExecutablePath();

