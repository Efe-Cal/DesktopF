# DesktopF
A lightweight Windows desktop search tool. Press Ctrl+F while the desktop is focused, search for an item, then press Enter to highlight its location.

![Screenshot](https://cdn.hackclub.com/019fe732-a57b-78e6-b085-96af94ca1278/Ekran%20g%C3%B6r%C3%BCnt%C3%BCs%C3%BC%202026-08-09%20182546.png)

## Requirements
- Windows 10 or later
- .NET 8 SDK

## Usage
- Ctrl+F - Open search
- ↑ / ↓ - Select a result
- Enter - Highlight matching desktop items
- Esc - Close the highlight or search window
- Gear icon - Configure prefix/contains, case-sensitive, or regex matching

### How to Run
1. Clone the repository
```
git clone https://github.com/Efe-Cal/DesktopF.git

```
2. Navigate to the project directory
```
cd DesktopF
```
3. Run the project
```
dotnet run
```

### Build to Executable
Run the following command to build a compressed, self-contained executable for Windows x64. The output will be `publish-single/DesktopF.exe`.
```powershell
dotnet publish .\DesktopF.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\publish-single
```

