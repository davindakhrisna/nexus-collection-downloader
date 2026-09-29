# Nexus Collection Downloader

A Windows click assistant for Vortex and Nexus Mods collection download prompts.

[Download the Windows executable](https://github.com/davindakhrisna/nexus-collection-downloader/releases/latest)

## Use

1. Put cropped Download button images for Vortex and your browser in one folder, then select that folder. Use PNG crops at your current display scale.
2. Set a fixed or random click interval and the browser kill delay. The default kill delay is 5.0 seconds.
3. Keep both windows visible and press Start. Restore the app from the taskbar to press Stop.

The app pauses if it cannot find the browser Download button. After clicking it, the app forcefully terminates the matched browser process, which may close other windows and tabs in that browser. It moves the mouse while clicking. Supported browsers include Chrome, Edge, Firefox, Brave, Opera, Vivaldi, and Chromium.

## Build

Requires the .NET 10 SDK:

```sh
dotnet publish -c Release -r win-x64 --self-contained true -o dist
```
