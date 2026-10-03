# NetForge

Modern lightweight network toolbox for Windows. Native C# / WinUI 3 / MVVM. No Electron, no WebView, no acrylic/mica.

## Build & run
Requirements: Windows 10 1809+ / Windows 11, Visual Studio 2022 with the ".NET desktop development" workload and the Windows App SDK (WinUI) tooling, .NET 8 SDK.

1. Open `NetForge.sln`, pick `x64` (or ARM64/x86), press F5.
2. Or: `dotnet build NetForge/NetForge.csproj -c Release -p:Platform=x64` then run `NetForge\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\NetForge.exe`.

Unpackaged and self-contained: no MSIX needed, no extra runtime install.

## Layout
- `Views/` XAML pages · `ViewModels/` MVVM · `Models/` · `Helpers/` (Loc, Elevation, ObservableObject)
- `Services/` all network logic behind interfaces (`IDnsService`, `IPingService`, `ITracerouteService`, `IAdapterService`, `INetworkTestService`, `ISpeedTestService`, `IWifiService`, `IDiagnosticsService`), wired in `AppServices`
- `Services/AdvancedCatalog.cs` every Advanced page = one read script + explicit actions (add a page by adding an entry)
- `Resources/<lang>/Resources.resw` en-US, zh-TW, zh-CN, ja-JP, ko-KR

## How it behaves
- Never runs as admin by default. Each system change shows a confirmation dialog with the exact PowerShell that will run; admin actions trigger one UAC prompt for that action only. The DNS page also offers "restart as administrator".
- DNS is backed up (`%LocalAppData%\NetForge\dns-backup.json`) before every change. Hosts is backed up to `%ProgramData%\NetForge` before every edit.
- Nothing runs in the background; pages cancel their work when you leave them.

## Honest limits
- **Speed test** uses Cloudflare's public speed endpoints through `ISpeedTestService`. Swap `AppServices.Speed` for another backend. If the server doesn't answer, the value stays blank.
- **Wi-Fi** is read via the native WLAN API. Fields Windows doesn't report show "Unavailable".
- **NetBIOS and QoS** are view-only. Windows exposes no safe general way to edit them here.
- **Language** changes apply after restarting the app. zh-CN / ja-JP / ko-KR cover navigation, buttons and status text; remaining strings (DNS provider blurbs, Advanced warnings) fall back to English. zh-TW and en-US are complete.
- DNS provider addresses were entered from memory of each provider's public docs: verify them before relying on them.
- The Advanced pages call PowerShell and `netsh`; a few read-outs (`route print`, `ipconfig /all`) use Windows' display language.
- **This code has not been compiled.** It was written without a Windows toolchain. Expect a few compile errors or XAML quirks on first build; the first run in Visual Studio is the real test.

## Build the exe without Visual Studio
Push this folder to a GitHub repo. The workflow in `.github/workflows/build.yml` builds it on a Windows runner.
Download `NetForge-win-x64` from the Actions run's Artifacts, unzip, and run `NetForge.exe` (keep the whole folder together).
Local alternative:
`dotnet publish NetForge/NetForge.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o publish`
