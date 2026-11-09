---
uid: Harp.Hobgoblin.Device
title: Registers
---

# Hobgoblin device

<div class="text-secondary d-flex flex-wrap column-gap-4">
  <dl><div><dt>WhoAmI</dt><dd>123</dd></div></dl>
  <dl><div><dt>Log files</dt><dd><code>Hobgoblin_&lt;address&gt;.bin</code></dd></div></dl>
</div>

Use the [Harp device pattern](https://harp-tech.org/articles/operators.html#device-pattern) to initialize the device, log data, and send commands to and receive messages from the Hobgoblin device.

## Registers

The core registers implemented by every Harp device, such as `WhoAmI` and `OperationControl`, are described in the [Device Registers and Operation specification](https://harp-tech.org/protocol/Device.html).

[!INCLUDE [](../../artifacts/docs/registers/registers.md)]
