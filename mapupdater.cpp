#pragma once
#include <windows.h>
#include <wininet.h>
#include <string>
#include <vector>
#include <sstream>
#include <fstream>

#pragma comment(lib, "wininet.lib")

// Map update structures
struct MapInfo {
    std::string filename;
    std::string mapname;
    int version;
};

struct MapUpdateProgress {
    int totalMaps;
    int currentMap;
    std::string currentMapName;
    std::string status;
    bool completed;
    bool cancelled;
};

// Global progress state
static MapUpdateProgress g_progress = {0, 0, "", "", false, false};
static HWND g_progressWindow = NULL;
static HWND g_progressBar = NULL;
static HWND g_statusText = NULL;

// Function declarations
bool DownloadFile(const std::string& url, const std::string& outputPath);
std::string DownloadString(const std::string& url);
std::vector<MapInfo> ParseMapIndex(const std::string& indexContent);
int GetLocalMapVersion(const std::string& versionFilePath);
void DownloadMapFiles(const std::string& baseUrl, const std::string& mapPath, const std::string& filename);
bool UpdateMaps(const std::string& isoPath, const std::string& region, HWND parentWindow);
LRESULT CALLBACK ProgressWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
DWORD WINAPI UpdateMapsThread(LPVOID param);

// Download a file from URL to local path
bool DownloadFile(const std::string& url, const std::string& outputPath) {
    HINTERNET hInternet = InternetOpenA("UYA Launcher", INTERNET_OPEN_TYPE_DIRECT, NULL, NULL, 0);
    if (!hInternet) return false;

    HINTERNET hUrl = InternetOpenUrlA(hInternet, url.c_str(), NULL, 0, INTERNET_FLAG_RELOAD, 0);
    if (!hUrl) {
        InternetCloseHandle(hInternet);
        return false;
    }

    // Create output file
    HANDLE hFile = CreateFileA(outputPath.c_str(), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hInternet);
        return false;
    }

    // Download in chunks
    char buffer[4096];
    DWORD bytesRead, bytesWritten;
    bool success = true;

    while (InternetReadFile(hUrl, buffer, sizeof(buffer), &bytesRead) && bytesRead > 0) {
        if (!WriteFile(hFile, buffer, bytesRead, &bytesWritten, NULL) || bytesWritten != bytesRead) {
            success = false;
            break;
        }
    }

    CloseHandle(hFile);
    InternetCloseHandle(hUrl);
    InternetCloseHandle(hInternet);

    // Delete file if download failed
    if (!success) {
        DeleteFileA(outputPath.c_str());
    }

    return success;
}

// Download a string from URL
std::string DownloadString(const std::string& url) {
    HINTERNET hInternet = InternetOpenA("UYA Launcher", INTERNET_OPEN_TYPE_DIRECT, NULL, NULL, 0);
    if (!hInternet) return "";

    HINTERNET hUrl = InternetOpenUrlA(hInternet, url.c_str(), NULL, 0, INTERNET_FLAG_RELOAD, 0);
    if (!hUrl) {
        InternetCloseHandle(hInternet);
        return "";
    }

    std::string result;
    char buffer[4096];
    DWORD bytesRead;

    while (InternetReadFile(hUrl, buffer, sizeof(buffer) - 1, &bytesRead) && bytesRead > 0) {
        buffer[bytesRead] = '\0';
        result += buffer;
    }

    InternetCloseHandle(hUrl);
    InternetCloseHandle(hInternet);

    return result;
}

// Parse map index file (format: filename|mapname|version)
std::vector<MapInfo> ParseMapIndex(const std::string& indexContent) {
    std::vector<MapInfo> maps;
    std::istringstream stream(indexContent);
    std::string line;

    while (std::getline(stream, line)) {
        if (line.empty()) continue;

        // Parse: filename|mapname|version
        size_t pos1 = line.find('|');
        if (pos1 == std::string::npos) continue;

        size_t pos2 = line.find('|', pos1 + 1);
        if (pos2 == std::string::npos) continue;

        MapInfo info;
        info.filename = line.substr(0, pos1);
        info.mapname = line.substr(pos1 + 1, pos2 - pos1 - 1);
        
        try {
            info.version = std::stoi(line.substr(pos2 + 1));
        } catch (...) {
            continue;
        }

        maps.push_back(info);
    }

    return maps;
}

// Get local map version from .version file
int GetLocalMapVersion(const std::string& versionFilePath) {
    HANDLE hFile = CreateFileA(versionFilePath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    if (hFile == INVALID_HANDLE_VALUE) return -1;

    DWORD bytesRead;
    int version = -1;
    if (ReadFile(hFile, &version, sizeof(int), &bytesRead, NULL) && bytesRead == sizeof(int)) {
        CloseHandle(hFile);
        return version;
    }

    CloseHandle(hFile);
    return -1;
}

// Download all files for a map
void DownloadMapFiles(const std::string& baseUrl, const std::string& mapPath, const std::string& filename) {
    const char* extensions[] = {".bg", ".thumb", ".map", ".world", ".sound", ".code", ".wad"};
    
    for (const char* ext : extensions) {
        std::string url = baseUrl + "/" + filename + ext;
        std::string outputPath = mapPath + "/" + filename + ext;
        DownloadFile(url, outputPath);
    }
}

// Progress window procedure
LRESULT CALLBACK ProgressWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_CLOSE:
            // Don't allow closing during download
            return 0;
            
        case WM_COMMAND:
            if (LOWORD(wParam) == IDCANCEL) {
                g_progress.cancelled = true;
            }
            break;
            
        default:
            return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    return 0;
}

// Thread function for updating maps
DWORD WINAPI UpdateMapsThread(LPVOID param) {
    struct ThreadParams {
        std::string isoPath;
        std::string region;
    };
    ThreadParams* params = (ThreadParams*)param;

    const std::string baseUrl = "https://box.rac-horizon.com/downloads/maps";
    
    // Determine index file based on region
    std::string indexFile;
    std::string regionExt;
    
    if (params->region == "NTSC") {
        indexFile = "index_uya_ntsc.txt";
        regionExt = "";
    } else if (params->region == "PAL") {
        indexFile = "index_uya_pal.txt";
        regionExt = ".pal";
    } else if (params->region == "Both") {
        // Handle both regions - we'll do NTSC first
        indexFile = "index_uya_ntsc.txt";
        regionExt = "";
    } else {
        g_progress.status = "Invalid region setting";
        g_progress.completed = true;
        delete params;
        return 1;
    }

    // Get ISO directory
    size_t lastSlash = params->isoPath.find_last_of("\\/");
    std::string isoDir = (lastSlash != std::string::npos) ? params->isoPath.substr(0, lastSlash) : ".";
    std::string mapsDir = isoDir + "\\uya";

    // Create maps directory
    CreateDirectoryA(mapsDir.c_str(), NULL);

    // Download and parse index
    g_progress.status = "Downloading map list...";
    SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)g_progress.status.c_str());

    std::string indexContent = DownloadString(baseUrl + "/" + indexFile);
    if (indexContent.empty()) {
        g_progress.status = "Failed to download map list";
        g_progress.completed = true;
        delete params;
        return 1;
    }

    std::vector<MapInfo> maps = ParseMapIndex(indexContent);
    
    // Check which maps need updating
    std::vector<MapInfo> mapsToUpdate;
    
    for (const MapInfo& map : maps) {
        std::string versionPath = mapsDir + "\\" + map.filename + ".version";
        int localVersion = GetLocalMapVersion(versionPath);
        
        if (localVersion < map.version) {
            mapsToUpdate.push_back(map);
        }
    }

    g_progress.totalMaps = mapsToUpdate.size();
    
    if (mapsToUpdate.empty()) {
        g_progress.status = "All maps are up to date!";
        SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)g_progress.status.c_str());
        Sleep(1000);
        g_progress.completed = true;
        delete params;
        return 0;
    }

    // Download updated maps
    SendMessage(g_progressBar, PBM_SETRANGE, 0, MAKELPARAM(0, g_progress.totalMaps));
    
    for (size_t i = 0; i < mapsToUpdate.size(); i++) {
        if (g_progress.cancelled) {
            g_progress.status = "Update cancelled";
            g_progress.completed = true;
            delete params;
            return 1;
        }

        const MapInfo& map = mapsToUpdate[i];
        g_progress.currentMap = i + 1;
        g_progress.currentMapName = map.mapname;
        
        std::string statusMsg = "Downloading " + map.mapname + " (" + 
                               std::to_string(i + 1) + "/" + std::to_string(g_progress.totalMaps) + ")";
        g_progress.status = statusMsg;
        SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)statusMsg.c_str());
        SendMessage(g_progressBar, PBM_SETPOS, i + 1, 0);

        // Download map files
        std::string filename = map.filename + regionExt;
        DownloadMapFiles(baseUrl + "/uya", mapsDir, filename);
        
        // Download version file
        std::string versionUrl = baseUrl + "/uya/" + map.filename + ".version";
        std::string versionPath = mapsDir + "\\" + map.filename + ".version";
        DownloadFile(versionUrl, versionPath);
    }

    // If "Both" region, also download PAL maps
    if (params->region == "Both") {
        indexFile = "index_uya_pal.txt";
        regionExt = ".pal";
        
        g_progress.status = "Downloading PAL map list...";
        SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)g_progress.status.c_str());
        
        indexContent = DownloadString(baseUrl + "/" + indexFile);
        if (!indexContent.empty()) {
            maps = ParseMapIndex(indexContent);
            mapsToUpdate.clear();
            
            for (const MapInfo& map : maps) {
                std::string versionPath = mapsDir + "\\" + map.filename + ".version";
                int localVersion = GetLocalMapVersion(versionPath);
                
                if (localVersion < map.version) {
                    mapsToUpdate.push_back(map);
                }
            }
            
            int previousTotal = g_progress.totalMaps;
            g_progress.totalMaps += mapsToUpdate.size();
            SendMessage(g_progressBar, PBM_SETRANGE, 0, MAKELPARAM(0, g_progress.totalMaps));
            
            for (size_t i = 0; i < mapsToUpdate.size(); i++) {
                if (g_progress.cancelled) break;

                const MapInfo& map = mapsToUpdate[i];
                g_progress.currentMap = previousTotal + i + 1;
                
                std::string statusMsg = "Downloading " + map.mapname + " (PAL) (" + 
                                       std::to_string(previousTotal + i + 1) + "/" + 
                                       std::to_string(g_progress.totalMaps) + ")";
                g_progress.status = statusMsg;
                SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)statusMsg.c_str());
                SendMessage(g_progressBar, PBM_SETPOS, previousTotal + i + 1, 0);

                std::string filename = map.filename + regionExt;
                DownloadMapFiles(baseUrl + "/uya", mapsDir, filename);
                
                std::string versionUrl = baseUrl + "/uya/" + map.filename + ".version";
                std::string versionPath = mapsDir + "\\" + map.filename + ".version";
                DownloadFile(versionUrl, versionPath);
            }
        }
    }

    g_progress.status = "Update complete!";
    SendMessage(g_statusText, WM_SETTEXT, 0, (LPARAM)g_progress.status.c_str());
    Sleep(1000);
    g_progress.completed = true;

    delete params;
    return 0;
}

// Main update function - shows progress window and downloads maps
bool UpdateMaps(const std::string& isoPath, const std::string& region, HWND parentWindow) {
    // Reset progress
    g_progress = {0, 0, "", "Initializing...", false, false};

    // Create progress window
    WNDCLASSEX wc = {0};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.lpfnWndProc = ProgressWndProc;
    wc.hInstance = GetModuleHandle(NULL);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = "MapUpdateProgress";
    RegisterClassEx(&wc);

    g_progressWindow = CreateWindowEx(
        0,
        "MapUpdateProgress",
        "Updating Custom Maps",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU,
        CW_USEDEFAULT, CW_USEDEFAULT, 500, 150,
        parentWindow,
        NULL,
        GetModuleHandle(NULL),
        NULL
    );

    if (!g_progressWindow) return false;

    // Create status text
    g_statusText = CreateWindowEx(
        0, "STATIC", "Initializing...",
        WS_CHILD | WS_VISIBLE | SS_CENTER,
        10, 20, 470, 30,
        g_progressWindow, NULL, GetModuleHandle(NULL), NULL
    );

    // Create progress bar
    g_progressBar = CreateWindowEx(
        0, PROGRESS_CLASS, NULL,
        WS_CHILD | WS_VISIBLE,
        10, 60, 470, 30,
        g_progressWindow, NULL, GetModuleHandle(NULL), NULL
    );

    ShowWindow(g_progressWindow, SW_SHOW);
    UpdateWindow(g_progressWindow);

    // Start update thread
    struct ThreadParams {
        std::string isoPath;
        std::string region;
    };
    ThreadParams* params = new ThreadParams{isoPath, region};
    
    HANDLE hThread = CreateThread(NULL, 0, UpdateMapsThread, params, 0, NULL);
    if (!hThread) {
        delete params;
        DestroyWindow(g_progressWindow);
        return false;
    }

    // Message loop until complete
    MSG msg;
    while (!g_progress.completed) {
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        Sleep(50);
    }

    WaitForSingleObject(hThread, INFINITE);
    CloseHandle(hThread);

    DestroyWindow(g_progressWindow);
    UnregisterClass("MapUpdateProgress", GetModuleHandle(NULL));

    return !g_progress.cancelled;
}
