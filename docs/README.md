# Harp Hobgoblin

The Harp Hobgoblin is a simple multi-purpose device for learning the basics of the Harp standard, and runs directly on a Raspberry Pi Pico or Raspberry Pi Pico 2. The repository contains working device metadata, firmware and software interfaces, and the device is meant to be adapted and modified for a variety of purposes.

## Documentation

The documentation, including how to assemble the device and install the firmware, is at https://harp-tech.org/device.hobgoblin.

## Software

- `Harp.Hobgoblin` on NuGet, the Bonsai and .NET interface.
- `harp-hobgoblin` on PyPI, the Python interface.

## Repository layout

- `device.yml`: the device register interface, from which the software interfaces are generated.
- `firmware`: the firmware, built with CMake and the Raspberry Pi Pico SDK.
- `software`: the .NET and Python interfaces.
- `docs`: the documentation website.

## Licensing

Each part of this repository is released under its own license. The `LICENSE` file at the root of the repository lists the licenses that apply to each folder.
