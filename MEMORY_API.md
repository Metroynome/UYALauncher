# Memory API Documentation

The UYALauncher now includes a powerful memory access system that allows you to read and write to PCSX2's emulated PS2 RAM directly from C++ code.

## Overview

The Memory API provides:
- **Direct memory access** to PS2 RAM through PCSX2's process
- **Automatic base address detection** via pattern scanning
- **Thread-safe operations** with mutex protection
- **Type-safe read/write functions** for various data types
- **Easy-to-use API** for game manipulation

## Quick Start

The memory system is automatically initialized 3 seconds after PCSX2 launches. You can use it anywhere in the launcher code.

### Basic Example

```cpp
#include "memory.h"

// Read a 32-bit value
uint32_t health;
if (Memory::Read32(0x001A0000, health)) {
    std::cout << "Player health: " << health << std::endl;
}

// Write a 32-bit value
if (Memory::Write32(0x001A0100, 999)) {
    std::cout << "Set ammo to 999" << std::endl;
}
```

## API Reference

### Initialization Functions

```cpp
bool Memory::Initialize(HANDLE processHandle);
```
Initializes the memory subsystem. Called automatically after PCSX2 launches.

```cpp
void Memory::Shutdown();
```
Shuts down the memory subsystem. Called automatically when PCSX2 closes.

```cpp
bool Memory::IsInitialized();
```
Returns true if the memory system is initialized and ready to use.

```cpp
bool Memory::RefreshMemoryBase();
```
Re-scans for the PS2 memory base address. Useful if initial scan failed.

### Read Functions

All read functions return `true` on success, `false` on failure.

```cpp
bool Memory::Read8(uint32_t ps2Address, uint8_t& outValue);
bool Memory::Read16(uint32_t ps2Address, uint16_t& outValue);
bool Memory::Read32(uint32_t ps2Address, uint32_t& outValue);
bool Memory::ReadFloat(uint32_t ps2Address, float& outValue);
bool Memory::ReadBuffer(uint32_t ps2Address, void* buffer, size_t size);
std::string Memory::ReadString(uint32_t ps2Address, size_t maxLength = 256);
```

### Write Functions

All write functions return `true` on success, `false` on failure.

```cpp
bool Memory::Write8(uint32_t ps2Address, uint8_t value);
bool Memory::Write16(uint32_t ps2Address, uint16_t value);
bool Memory::Write32(uint32_t ps2Address, uint32_t value);
bool Memory::WriteFloat(uint32_t ps2Address, float value);
bool Memory::WriteBuffer(uint32_t ps2Address, const void* buffer, size_t size);
bool Memory::WriteString(uint32_t ps2Address, const std::string& str);
```

### Utility Functions

```cpp
uintptr_t Memory::GetPS2MemoryBase();
```
Returns the base address of PS2 RAM in PCSX2's process memory.

```cpp
bool Memory::IsValidPS2Address(uint32_t ps2Address);
```
Checks if a PS2 address is within valid RAM range (0x00000000 - 0x01FFFFFF).

## Usage Examples

### Example 1: Infinite Health Cheat

```cpp
#include "memory.h"
#include <thread>

void InfiniteHealthCheat() {
    const uint32_t HEALTH_ADDRESS = 0x001A0000; // Replace with actual address
    const uint32_t MAX_HEALTH = 999;
    
    while (Memory::IsInitialized()) {
        Memory::Write32(HEALTH_ADDRESS, MAX_HEALTH);
        Sleep(100); // Update every 100ms
    }
}

// Start in a background thread
std::thread(InfiniteHealthCheat).detach();
```

### Example 2: Read Player Position

```cpp
#include "memory.h"

struct Vector3 {
    float x, y, z;
};

bool GetPlayerPosition(Vector3& pos) {
    const uint32_t POSITION_ADDRESS = 0x001A0200; // Replace with actual address
    
    if (!Memory::ReadFloat(POSITION_ADDRESS + 0, pos.x)) return false;
    if (!Memory::ReadFloat(POSITION_ADDRESS + 4, pos.y)) return false;
    if (!Memory::ReadFloat(POSITION_ADDRESS + 8, pos.z)) return false;
    
    return true;
}

// Usage
Vector3 playerPos;
if (GetPlayerPosition(playerPos)) {
    std::cout << "Player at: " << playerPos.x << ", " 
              << playerPos.y << ", " << playerPos.z << std::endl;
}
```

### Example 3: Modify Multiple Values

```cpp
#include "memory.h"

struct PlayerStats {
    uint32_t health;
    uint32_t armor;
    uint32_t ammo;
    uint32_t bolts;
};

void MaxOutPlayer() {
    const uint32_t STATS_ADDRESS = 0x001A0000; // Replace with actual address
    
    PlayerStats maxStats = {
        999,    // health
        999,    // armor
        999,    // ammo
        999999  // bolts
    };
    
    if (Memory::WriteBuffer(STATS_ADDRESS, &maxStats, sizeof(maxStats))) {
        std::cout << "Player stats maxed out!" << std::endl;
    }
}
```

### Example 4: Hotkey-Triggered Cheat

Add this to `main.cpp` in the `HandleHotkey` function:

```cpp
case HOTKEY_INFINITE_HEALTH: {
    if (consoleEnabled)
        std::cout << "Infinite health activated!" << std::endl;
    
    const uint32_t HEALTH_ADDRESS = 0x001A0000;
    Memory::Write32(HEALTH_ADDRESS, 999);
    break;
}
```

Don't forget to register the hotkey in `RegisterHotkeys()`:

```cpp
#define HOTKEY_INFINITE_HEALTH 3
RegisterHotKey(NULL, HOTKEY_INFINITE_HEALTH, MOD_NOREPEAT, VK_F9);
```

### Example 5: Continuous Memory Monitor

```cpp
#include "memory.h"
#include <thread>

void MonitorGameState() {
    const uint32_t GAME_STATE_ADDRESS = 0x001A0000;
    uint32_t lastState = 0;
    
    while (Memory::IsInitialized()) {
        uint32_t currentState;
        if (Memory::Read32(GAME_STATE_ADDRESS, currentState)) {
            if (currentState != lastState) {
                std::cout << "Game state changed: " << currentState << std::endl;
                lastState = currentState;
            }
        }
        Sleep(500); // Check every 500ms
    }
}

// Start monitoring in background
std::thread(MonitorGameState).detach();
```

### Example 6: Read/Write Strings

```cpp
#include "memory.h"

void ModifyPlayerName() {
    const uint32_t NAME_ADDRESS = 0x001A0000; // Replace with actual address
    
    // Read current name
    std::string currentName = Memory::ReadString(NAME_ADDRESS, 32);
    std::cout << "Current name: " << currentName << std::endl;
    
    // Write new name
    if (Memory::WriteString(NAME_ADDRESS, "Hacker")) {
        std::cout << "Name changed to 'Hacker'" << std::endl;
    }
}
```

## Finding Memory Addresses

To find game memory addresses, you can use tools like:

1. **Cheat Engine** - Popular memory scanner for Windows
2. **PCSX2 Debugger** - Built-in debugging tools
3. **Your existing knowledge** - If you have addresses from other sources

### Tips for Finding Addresses:

1. Use Cheat Engine to attach to PCSX2 process
2. Search for known values (health, ammo, etc.)
3. Change the value in-game and scan again
4. Repeat until you find the address
5. The address you find in Cheat Engine is the **PS2 address** (use it directly in the API)

## Advanced Usage

### Creating a Cheat Manager

```cpp
#include "memory.h"
#include <map>
#include <functional>

class CheatManager {
private:
    std::map<std::string, std::function<void()>> cheats;
    bool running = false;
    std::thread updateThread;
    
public:
    void RegisterCheat(const std::string& name, std::function<void()> cheatFunc) {
        cheats[name] = cheatFunc;
    }
    
    void Start() {
        running = true;
        updateThread = std::thread([this]() {
            while (running && Memory::IsInitialized()) {
                for (auto& [name, func] : cheats) {
                    func();
                }
                Sleep(100);
            }
        });
    }
    
    void Stop() {
        running = false;
        if (updateThread.joinable()) {
            updateThread.join();
        }
    }
};

// Usage
CheatManager cheatMgr;
cheatMgr.RegisterCheat("InfiniteHealth", []() {
    Memory::Write32(0x001A0000, 999);
});
cheatMgr.RegisterCheat("InfiniteAmmo", []() {
    Memory::Write32(0x001A0100, 999);
});
cheatMgr.Start();
```

## Thread Safety

All Memory API functions are thread-safe. You can safely call them from:
- Main thread
- Hotkey handlers
- Background threads
- Multiple threads simultaneously

## Error Handling

Always check return values:

```cpp
uint32_t value;
if (Memory::Read32(address, value)) {
    // Success - use value
} else {
    // Failed - memory system not initialized or invalid address
    std::cout << "Failed to read memory" << std::endl;
}
```

## Performance Considerations

- Reading/writing memory is relatively fast (microseconds)
- Avoid excessive operations in tight loops
- Use `Sleep()` or timers for continuous monitoring
- Batch operations when possible using `ReadBuffer`/`WriteBuffer`

## Troubleshooting

### Memory system not initializing?

1. Check console output for error messages
2. Ensure PCSX2 is fully started (wait a few seconds)
3. Try calling `Memory::RefreshMemoryBase()` manually
4. Enable console output in config.ini: `ShowConsole=true`

### Reads/writes failing?

1. Verify the PS2 address is correct
2. Check if address is within valid range (0x00000000 - 0x01FFFFFF)
3. Ensure PCSX2 is still running
4. Try reading a known good address first

### Base address not found?

The scanner looks for 32MB+ memory regions. If it fails:
1. Wait longer for PCSX2 to initialize
2. Try different PCSX2 versions
3. Check if PCSX2 is using unusual memory configurations

## Integration Points

The memory system is integrated at these points:

1. **Initialization**: `pcsx2.cpp` - `LaunchPCSX2()` (3 second delay)
2. **Cleanup**: `pcsx2.cpp` - `CleanupPCSX2()` and `TerminatePCSX2()`
3. **Available everywhere**: Just `#include "memory.h"`

## Example: Complete Cheat System

Here's a complete example you can add to your launcher:

```cpp
// In a new file: src/cheats.cpp
#include "memory.h"
#include <thread>
#include <atomic>

namespace Cheats {
    static std::atomic<bool> g_cheatsEnabled(false);
    static std::thread g_cheatThread;
    
    // Cheat addresses (replace with actual UYA addresses)
    constexpr uint32_t ADDR_HEALTH = 0x001A0000;
    constexpr uint32_t ADDR_AMMO = 0x001A0100;
    constexpr uint32_t ADDR_BOLTS = 0x001A0200;
    
    void CheatLoop() {
        while (g_cheatsEnabled && Memory::IsInitialized()) {
            // Infinite health
            Memory::Write32(ADDR_HEALTH, 999);
            
            // Infinite ammo
            Memory::Write32(ADDR_AMMO, 999);
            
            // Max bolts
            Memory::Write32(ADDR_BOLTS, 999999);
            
            Sleep(100); // Update every 100ms
        }
    }
    
    void Enable() {
        if (!g_cheatsEnabled) {
            g_cheatsEnabled = true;
            g_cheatThread = std::thread(CheatLoop);
            std::cout << "[Cheats] Enabled!" << std::endl;
        }
    }
    
    void Disable() {
        if (g_cheatsEnabled) {
            g_cheatsEnabled = false;
            if (g_cheatThread.joinable()) {
                g_cheatThread.join();
            }
            std::cout << "[Cheats] Disabled!" << std::endl;
        }
    }
    
    void Toggle() {
        if (g_cheatsEnabled) {
            Disable();
        } else {
            Enable();
        }
    }
}

// Add to main.cpp hotkey handler:
// case HOTKEY_TOGGLE_CHEATS:
//     Cheats::Toggle();
//     break;
```

## License

This memory system is part of UYALauncher and follows the same license.

## Support

For issues or questions:
1. Check console output with `ShowConsole=true`
2. Verify PCSX2 is running and game is loaded
3. Test with known good addresses first
4. Report bugs with detailed logs
