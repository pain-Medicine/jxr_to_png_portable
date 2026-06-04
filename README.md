# jxr_to_png GUI Wrapper

Windows WPF application wrapper for `jxr_to_png`. 

Builds into a portable, single-file executable

## Prerequisites
* .NET 5 SDK (or newer) installed on your system.

## How to Build the Portable EXE

1. Ensure the compiled C++ converter engine `jxr_to_png.exe` exists in the `jxr_to_png_` directory (at the repository root):
   ```
   jxr_to_png/
   ├── jxr_to_png_/
   │   └── jxr_to_png.exe
   ├── gui/
   ```

2. Open a PowerShell console, navigate to the `gui` folder, and run the build script:
   ```powershell
   ./build_gui.ps1
   ```

3. The script will compile the code and produce a single standalone executable at the repository root:
   ```
   jxr_to_png/dist/JxrConverter.exe
   ```
