#pragma once
#include <windows.h>
#include <cstdint>
#include <string>

namespace Memory {
    // Initialization and cleanup
    bool Initialize(HANDLE processHandle);
    void Shutdown();
    bool IsInitialized();
    
    // Core read functions (returns true on success)
    bool Read8(uint32_t ps2Address, uint8_t& outValue);
    bool Read16(uint32_t ps2Address, uint16_t& outValue);
    bool Read32(uint32_t ps2Address, uint32_t& outValue);
    bool ReadFloat(uint32_t ps2Address, float& outValue);
    bool ReadBuffer(uint32_t ps2Address, void* buffer, size_t size);
    
    // Core write functions (returns true on success)
    bool Write8(uint32_t ps2Address, uint8_t value);
    bool Write16(uint32_t ps2Address, uint16_t value);
    bool Write32(uint32_t ps2Address, uint32_t value);
    bool WriteFloat(uint32_t ps2Address, float value);
    bool WriteBuffer(uint32_t ps2Address, const void* buffer, size_t size);
    
    // Utility functions
    std::string ReadString(uint32_t ps2Address, size_t maxLength = 256);
    bool WriteString(uint32_t ps2Address, const std::string& str);
    
    // Advanced functions
    uintptr_t GetPS2MemoryBase();
    bool IsValidPS2Address(uint32_t ps2Address);
    bool RefreshMemoryBase(); // Re-scan for memory base if needed
}
