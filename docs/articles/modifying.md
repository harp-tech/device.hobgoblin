# Modifying the device

The Hobgoblin is meant to be adapted. Its register interface is declared in `device.yml` at the root of the repository, and the C# and Python interfaces are generated from it.

## Editing device metadata

The `device.yml` file contains the Hobgoblin device metadata. The file must specify every device register, including bit masks, group masks and payload formats. The [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) for Visual Studio Code validates the file against the schema declared in its first lines.

The firmware registers are written by hand in `firmware/src/main.cpp`, so a change to `device.yml` also needs a matching change to the register specifications there.

## Generating the device interfaces

The interfaces are generated with the [Harp toolkit](https://harp-tech.org/toolkit):

```text
dotnet tool restore
dotnet harp.toolkit generate interface csharp -o software/dotnet/Harp.Hobgoblin
dotnet harp.toolkit generate interface python --package -o software/python/src/harp/device/hobgoblin
```
