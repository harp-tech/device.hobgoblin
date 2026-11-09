#ifndef CONFIG_H
#define CONFIG_H

#include <cstddef>
#include <cstdint>
#include <core_registers.h>

// Device identity. Versions and the interface hash are set by the build.
inline constexpr uint16_t HARP_DEVICE_ID = 123;
inline constexpr semver_t FW_VERSION = {FW_VERSION_MAJOR, FW_VERSION_MINOR, FW_VERSION_PATCH};
inline constexpr semver_t HW_VERSION = {HW_VERSION_MAJOR, HW_VERSION_MINOR, HW_VERSION_PATCH};
inline constexpr uint8_t HARP_INTERFACE_HASH[20] = INTERFACE_HASH;

// Pins.
inline constexpr uint32_t DO0_PIN = 15;
inline constexpr uint32_t DI_MASK = 0x700C;
inline constexpr uint32_t DO_MASK = 0xFF << DO0_PIN;
inline constexpr uint32_t AI0_PIN = 26;
inline constexpr uint32_t AI1_PIN = 27;
inline constexpr uint32_t AI2_PIN = 28;
inline constexpr uint32_t AI_MASK = 0x7;
inline constexpr size_t HARP_SYNC_RX_PIN = 5;

#endif // CONFIG_H
