# Building the firmware

The firmware is built with CMake and the [Raspberry Pi Pico SDK](https://www.raspberrypi.com/documentation/microcontrollers/c_sdk.html), and uses the Harp [core.pico](https://harp-tech.org/core.pico/) library as a submodule. Fetch it after cloning the repository:

```text
git submodule update --init --recursive
```

## With Visual Studio Code

1. Install the [Raspberry Pi Pico extension](https://marketplace.visualstudio.com/items?itemName=raspberry-pi.raspberry-pi-pico).
2. Run **Raspberry Pi Pico: Import Pico Project** from the command palette, and select the `firmware` folder.
3. Run **Raspberry Pi Pico: Compile Pico Project**.

The extension builds the image for either the Raspberry Pi Pico or the Pico 2, selected in the status bar.

## From the command line

A command-line build needs CMake, Ninja, the Arm GNU toolchain, the Pico SDK and picotool. Point `PICO_SDK_PATH` at the Pico SDK, then build the release image for both the Pico and the Pico 2 from the root of the repository:

```text
cmake -S firmware/release -B artifacts/firmware -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts/firmware
```

The image is written to `artifacts/firmware/bin`. Its name follows the convention that `harp.toolkit update` expects, so it can be used to update a device as described in [Installation](installation.md#updating-the-firmware). The name ends in `-preview0`, which marks it as a local build.
