# Harp Hobgoblin

A simple multi-purpose device for learning the basics of the Harp standard. Although the repository contains working device metadata, firmware and high-level interface, the Harp Hobgoblin device is made to be adapted and modified for a variety of purposes.

## Assembling the device

The Harp Hobgoblin is designed to operate directly from a [Raspberry Pi Pico](https://www.raspberrypi.com/products/raspberry-pi-pico/) / [Pico 2](https://www.raspberrypi.com/products/raspberry-pi-pico-2/) board. To make it easier to interface with a variety of inputs and outputs we recommend the [Gravity: Expansion Board](https://www.dfrobot.com/product-2393.html).

> [!NOTE]
> The following links are provided as a reference only. We are not connected to or in any other way affiliated with the suppliers listed below.

  - Raspberry Pi Pico:
    - [Raspberry Pi Pico (with headers)](https://thepihut.com/products/raspberry-pi-pico?src=raspberrypi&variant=41925332566211)
    - [Raspberry Pi Pico 2 (with headers)](https://thepihut.com/products/raspberry-pi-pico-2?variant=54063366701441)
  - Gravity Sensors:
    - [Gravity: Expansion Board for Raspberry Pi Pico / Pico 2](https://www.dfrobot.com/product-2393.html)
    - [Gravity: 9 PCS Sensor Set for Arduino](https://www.dfrobot.com/product-110.html)
    - [Gravity: 27 PCS Sensor Set for Arduino](https://www.dfrobot.com/product-725.html)

## Flashing the firmware

1. Press-and-hold the Pico BOOTSEL button while you connect the device to the computer USB port.
2. Drag-and-drop the `.uf2` file matching your Pico board into the new storage device that appears on your PC.

## Using the device

Harp Hobgoblin is designed for use with the [Bonsai](https://bonsai-rx.org/) visual reactive programming language.

1. Install the `Harp.Hobgoblin` package using the [Bonsai package manager](https://bonsai-rx.org/docs/articles/packages.html).
2. Insert the `Device` source from the editor toolbox.
3. For additional documentation and examples, refer to the [official Harp documentation](https://harp-tech.org/articles/operators.html).

## Building the firmware

### Prerequisites

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [Raspberry Pi Pico extension](https://marketplace.visualstudio.com/items?itemName=raspberry-pi.raspberry-pi-pico).
3. Update the Harp Core submodule:
```
git submodule update --init --recursive
```

Follow the steps outlined in [Firmware/README.md](/Firmware/README.md).

## Editing device metadata

### Prerequisites

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml).

The `device.yml` file in the root of the project contains the Hobgoblin device metadata. A complete specification of all device registers, including bit masks, group masks, and payload formats needs to be provided.

## Generating the device interface

### Prerequisites

1. Install [`dotnet`](https://dotnet.microsoft.com/).
2. Install `dotnet-t4`.
```
dotnet tool install -g dotnet-t4
```

The `Generators` folder contains all text templates and project files required to generate both the firmware headers and the interface for the Hobgoblin device. To run the text templating engine just build the project inside this folder.

```
dotnet build Generators
```
