# Installation

This page covers the software packages for the Hobgoblin device, as well as how to install and update its firmware.

## Software packages

# [Bonsai](#tab/bonsai)

The Harp Hobgoblin is designed for use with the [Bonsai](https://bonsai-rx.org/) visual reactive programming language.

- Download and install [Bonsai](https://bonsai-rx.org/docs/articles/installation.html).
- Install the `Harp.Hobgoblin` package using the [Bonsai package manager](https://bonsai-rx.org/docs/articles/packages.html).

To test the device, follow the first workflow in [Bonsai](bonsai.md).

# [Python](#tab/python)

The `harp-hobgoblin` package provides the Python interface for the device:

```text
pip install harp-hobgoblin
```

---

## Firmware

Firmware updates are published on the [release page](https://github.com/harp-tech/device.hobgoblin/releases) of the device repository. Each release includes a single firmware image, a `.uf2` file named `Hobgoblin-fw<version>-harp<version>-hw<version>-ass0.uf2`. The same image runs on both the Raspberry Pi Pico and the Raspberry Pi Pico 2, since it carries a separate copy of the firmware for each chip.

### Installing the firmware for the first time

1. Press and hold the BOOTSEL button of the Raspberry Pi Pico while connecting it to a USB port of the computer.
2. Copy the firmware image into the new storage device that appears on the computer.

The device restarts with the new firmware as soon as the copy completes.

### Updating the firmware

A device that is already running Hobgoblin firmware can be updated from the command line with the [Harp toolkit](https://harp-tech.org/toolkit):

```text
dotnet tool install --global harp.toolkit
harp.toolkit update Hobgoblin-fw0.2-harp2.0-hw1.0-ass0.uf2 --port COM3
```

Updating some devices needs a driver or system package installed once on the computer, as described in [Devices built on the Pico core](https://harp-tech.org/toolkit/articles/update.html#devices-built-on-the-pico-core).
