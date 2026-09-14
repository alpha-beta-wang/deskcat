# Native Mochi runtime

The Electron prototype remains in `src/` only as a reference. Production builds
use small native, borderless windows and pre-rendered PNG frames:

- Windows: `native/windows` is a .NET/WinForms layered window. It redraws only
  a 200×135 bitmap, has no Chromium child processes, and wakes at 4 Hz while
  still, briefly increasing cadence only for a blink or a walk.
- macOS: `native/macos` is an AppKit accessory application with an `NSPanel` and
  a menu-bar control. Build it on macOS with `cd native/macos && ./build.sh`.

Windows build:

```powershell
dotnet publish native/windows/Mochi.Native.csproj -c Release -o dist/Mochi.Native-win-x64
```
