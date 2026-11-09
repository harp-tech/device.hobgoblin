# Overview

The Harp Hobgoblin is a simple multi-purpose device for learning the basics of the Harp standard. The repository contains working device metadata, firmware and software interfaces, and the device is meant to be adapted and modified for a variety of purposes.

The Hobgoblin runs directly on a [Raspberry Pi Pico](https://www.raspberrypi.com/products/raspberry-pi-pico/) or [Raspberry Pi Pico 2](https://www.raspberrypi.com/products/raspberry-pi-pico-2/), and provides:

- Five digital inputs reported whenever any of them changes.
- Eight digital outputs that can be set, cleared and toggled, or generate pulse trains.
- Three analog inputs sampled continuously.
- Hardware timestamping and synchronization with other [Harp](https://harp-tech.org/articles/about.html) devices.
- [Bonsai](https://bonsai-rx.org/) integration for flexible experiment acquisition and control.

## Getting a device

The Hobgoblin is assembled from off-the-shelf parts. Follow [Assembling the device](articles/assembly.md) to put one together, then [install the software and firmware](articles/installation.md).

[!INCLUDE [](includes/version-footer.md)]
