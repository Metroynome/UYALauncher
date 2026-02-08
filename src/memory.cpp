#include "memory.h"
#include <iostream>
#include <vector>
#include <mutex>
#include <psapi.h>

namespace Memory {
    // Internal state
    static HANDLE g_processHandle = NULL;
    static uintptr_t g_ps2MemoryBase = 0;
    static std::mutex g_memoryMutex;
    static bool g_initialized = false;
    
    // PS2 memory constants
    constexpr uint32_t PS2_RAM_SIZE = 0x02000000;  // 32MB
    constexpr uint32_t PS2_RAM_MASK = 0x01FFFFFF;  // Address mask
    
    // Pattern scanning helper
    static uintptr_t FindPattern(HANDLE hProcess, const char* pattern, const char* mask, uintptr_t start, size_t searchSize) {
        size_t patternLen = strlen(mask);
        std::vector<uint8_t> buffer(searchSize);
        
        SIZE_T bytesRead;
        if (!ReadProcessMemory(hProcess, (LPCVOID)start, buffer.data(), searchSize, &bytesRead)) {
            return 0;
        }
        
        for (size_t i = 0; i < bytesRead - patternLen; i++) {
            bool found = true;
            for (size_t j = 0; j < patternLen; j++) {
                if (mask[j] == 'x' && buffer[i + j] != (uint8_t)pattern[j]) {
                    found = false;
                    break;
                }
            }
            if (found) {
                return start + i;
            }
        }
        
        return 0;
    }
    
    // Scan for PS2 memory base address
    static bool ScanForPS2Memory(HANDLE hProcess) {
        std::cout << "[Memory] Scanning for PS2 RAM base address..." << std::endl;
        
        // Get process memory info
        SYSTEM_INFO sysInfo;
        GetSystemInfo(&sysInfo);
        
        MEMORY_BASIC_INFORMATION memInfo;
        uintptr_t address = (uintptr_t)sysInfo.lpMinimumApplicationAddress;
        uintptr_t maxAddress = (uintptr_t)sysInfo.lpMaximumApplicationAddress;
        
        // Look for large committed memory regions (PS2 RAM is 32MB)
        while (address < maxAddress) {
            if (VirtualQueryEx(hProcess, (LPCVOID)address, &memInfo, sizeof(memInfo)) == 0) {
                break;
            }
            
            // Check if this region could be PS2 RAM
            if (memInfo.State == MEM_COMMIT && 
                memInfo.Type == MEM_PRIVATE &&
                memInfo.RegionSize >= PS2_RAM_SIZE &&
                (memInfo.Protect == PAGE_READWRITE || memInfo.Protect == PAGE_EXECUTE_READWRITE)) {
                
                // Verify by checking if we can read from it
                uint8_t testByte;
                SIZE_T bytesRead;
                if (ReadProcessMemory(hProcess, (LPCVOID)address, &testByte, 1, &bytesRead) && bytesRead == 1) {
                    std::cout << "[Memory] Found potential PS2 RAM at: 0x" << std::hex << address << std::dec << std::endl;
                    std::cout << "[Memory] Region size: " << (memInfo.RegionSize / 1024 / 1024) << " MB" << std::endl;
                    
                    g_ps2MemoryBase = address;
                    return true;
                }
            }
            
            address += memInfo.RegionSize;
        }
        
        std::cout << "[Memory] Failed to find PS2 RAM base address" << std::endl;
        return false;
    }
    
    // Alternative: Scan PCSX2 modules for memory base
    static bool ScanPCSX2Modules(HANDLE hProcess) {
        std::cout << "[Memory] Scanning PCSX2 modules..." << std::endl;
        
        HMODULE hMods[1024];
        DWORD cbNeeded;
        
        if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded)) {
            for (unsigned int i = 0; i < (cbNeeded / sizeof(HMODULE)); i++) {
                char szModName[MAX_PATH];
                if (GetModuleFileNameExA(hProcess, hMods[i], szModName, sizeof(szModName))) {
                    // Check if this is the main PCSX2 executable
                    std::string modName = szModName;
                    if (modName.find("pcsx2") != std::string::npos || 
                        modName.find("PCSX2") != std::string::npos) {
                        
                        MODULEINFO modInfo;
                        if (GetModuleInformation(hProcess, hMods[i], &modInfo, sizeof(modInfo))) {
                            std::cout << "[Memory] Scanning module: " << szModName << std::endl;
                            std::cout << "[Memory] Base: 0x" << std::hex << (uintptr_t)modInfo.lpBaseOfDll 
                                     << " Size: " << std::dec << (modInfo.SizeOfImage / 1024) << " KB" << std::endl;
                            
                            // Scan this module's memory space
                            if (ScanForPS2Memory(hProcess)) {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        
        return false;
    }
    
    // Public API Implementation
    
    bool Initialize(HANDLE processHandle) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (g_initialized) {
            std::cout << "[Memory] Already initialized" << std::endl;
            return true;
        }
        
        if (!processHandle || processHandle == INVALID_HANDLE_VALUE) {
            std::cout << "[Memory] Invalid process handle" << std::endl;
            return false;
        }
        
        g_processHandle = processHandle;
        
        std::cout << "[Memory] Initializing memory subsystem..." << std::endl;
        
        // Try to find PS2 memory base
        if (!ScanForPS2Memory(processHandle)) {
            std::cout << "[Memory] Primary scan failed, trying module scan..." << std::endl;
            if (!ScanPCSX2Modules(processHandle)) {
                std::cout << "[Memory] Failed to locate PS2 memory base" << std::endl;
                std::cout << "[Memory] Memory functions will not work until base is found" << std::endl;
                // Don't fail initialization - allow manual refresh later
            }
        }
        
        g_initialized = true;
        std::cout << "[Memory] Memory subsystem initialized" << std::endl;
        
        if (g_ps2MemoryBase != 0) {
            std::cout << "[Memory] PS2 RAM Base: 0x" << std::hex << g_ps2MemoryBase << std::dec << std::endl;
        }
        
        return true;
    }
    
    void Shutdown() {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        std::cout << "[Memory] Shutting down memory subsystem..." << std::endl;
        
        g_processHandle = NULL;
        g_ps2MemoryBase = 0;
        g_initialized = false;
    }
    
    bool IsInitialized() {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        return g_initialized;
    }
    
    bool RefreshMemoryBase() {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        std::cout << "[Memory] Refreshing memory base address..." << std::endl;
        g_ps2MemoryBase = 0;
        
        return ScanForPS2Memory(g_processHandle);
    }
    
    uintptr_t GetPS2MemoryBase() {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        return g_ps2MemoryBase;
    }
    
    bool IsValidPS2Address(uint32_t ps2Address) {
        // PS2 addresses are typically in the range 0x00000000 - 0x01FFFFFF
        return (ps2Address & ~PS2_RAM_MASK) == 0;
    }
    
    // Helper to translate PS2 address to PCSX2 process address
    static uintptr_t TranslateAddress(uint32_t ps2Address) {
        if (g_ps2MemoryBase == 0) {
            return 0;
        }
        
        // Mask the address to valid PS2 RAM range
        uint32_t offset = ps2Address & PS2_RAM_MASK;
        
        // Check if offset is within PS2 RAM size
        if (offset >= PS2_RAM_SIZE) {
            return 0;
        }
        
        return g_ps2MemoryBase + offset;
    }
    
    // Read implementations
    
    bool Read8(uint32_t ps2Address, uint8_t& outValue) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesRead;
        return ReadProcessMemory(g_processHandle, (LPCVOID)address, &outValue, sizeof(uint8_t), &bytesRead) 
               && bytesRead == sizeof(uint8_t);
    }
    
    bool Read16(uint32_t ps2Address, uint16_t& outValue) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesRead;
        return ReadProcessMemory(g_processHandle, (LPCVOID)address, &outValue, sizeof(uint16_t), &bytesRead) 
               && bytesRead == sizeof(uint16_t);
    }
    
    bool Read32(uint32_t ps2Address, uint32_t& outValue) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesRead;
        return ReadProcessMemory(g_processHandle, (LPCVOID)address, &outValue, sizeof(uint32_t), &bytesRead) 
               && bytesRead == sizeof(uint32_t);
    }
    
    bool ReadFloat(uint32_t ps2Address, float& outValue) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesRead;
        return ReadProcessMemory(g_processHandle, (LPCVOID)address, &outValue, sizeof(float), &bytesRead) 
               && bytesRead == sizeof(float);
    }
    
    bool ReadBuffer(uint32_t ps2Address, void* buffer, size_t size) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle || !buffer || size == 0) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesRead;
        return ReadProcessMemory(g_processHandle, (LPCVOID)address, buffer, size, &bytesRead) 
               && bytesRead == size;
    }
    
    // Write implementations
    
    bool Write8(uint32_t ps2Address, uint8_t value) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesWritten;
        return WriteProcessMemory(g_processHandle, (LPVOID)address, &value, sizeof(uint8_t), &bytesWritten) 
               && bytesWritten == sizeof(uint8_t);
    }
    
    bool Write16(uint32_t ps2Address, uint16_t value) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesWritten;
        return WriteProcessMemory(g_processHandle, (LPVOID)address, &value, sizeof(uint16_t), &bytesWritten) 
               && bytesWritten == sizeof(uint16_t);
    }
    
    bool Write32(uint32_t ps2Address, uint32_t value) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesWritten;
        return WriteProcessMemory(g_processHandle, (LPVOID)address, &value, sizeof(uint32_t), &bytesWritten) 
               && bytesWritten == sizeof(uint32_t);
    }
    
    bool WriteFloat(uint32_t ps2Address, float value) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesWritten;
        return WriteProcessMemory(g_processHandle, (LPVOID)address, &value, sizeof(float), &bytesWritten) 
               && bytesWritten == sizeof(float);
    }
    
    bool WriteBuffer(uint32_t ps2Address, const void* buffer, size_t size) {
        std::lock_guard<std::mutex> lock(g_memoryMutex);
        
        if (!g_initialized || !g_processHandle || !buffer || size == 0) {
            return false;
        }
        
        uintptr_t address = TranslateAddress(ps2Address);
        if (address == 0) {
            return false;
        }
        
        SIZE_T bytesWritten;
        return WriteProcessMemory(g_processHandle, (LPVOID)address, buffer, size, &bytesWritten) 
               && bytesWritten == size;
    }
    
    // Utility implementations
    
    std::string ReadString(uint32_t ps2Address, size_t maxLength) {
        std::vector<char> buffer(maxLength + 1, 0);
        
        if (!ReadBuffer(ps2Address, buffer.data(), maxLength)) {
            return "";
        }
        
        // Ensure null termination
        buffer[maxLength] = '\0';
        
        return std::string(buffer.data());
    }
    
    bool WriteString(uint32_t ps2Address, const std::string& str) {
        // Write string including null terminator
        return WriteBuffer(ps2Address, str.c_str(), str.length() + 1);
    }
}
