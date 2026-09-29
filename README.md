# Nexus Collection Downloader

A Windows click assistant for Vortex and Nexus Mods collection download prompts.

[Download the Windows executable](https://github.com/davindakhrisna/nexus-collection-downloader/releases/latest)

## Use

1. Select screenshots of the Vortex Download button, browser Download button, and browser window Close button. Use tight PNG crops at your current display scale.
2. Set a fixed or random click interval and the browser close delay. The default close delay is 5.2 seconds.
3. Keep both windows visible and press Start. Restore the app from the taskbar to press Stop.

The app pauses if it cannot find the browser Download or Close button. It moves the mouse while clicking.

## Build

Requires the .NET 10 SDK:

```sh
dotnet publish -c Release -r win-x64 --self-contained true -o dist
```
