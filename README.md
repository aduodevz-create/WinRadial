# WinRadial

A macOS-style radial "pie menu" overlay launcher for Windows. Trigger a circular HUD with a global hotkey to quickly access system actions, launch apps, open folders, and more.

![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6?style=flat-square&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## Features

- **8-wedge radial menu** with smooth hover detection and animations
- **Global hotkey** (default `Ctrl+Alt+Space`) — opens instantly at cursor position
- **Multi-monitor aware** — always opens on the correct monitor
- **Paginated categories** — Left/Right arrows cycle through action groups
- **Nested submenu rings** — wedges with children expand to a concentric outer ring
- **Built-in system actions**: Lock, Sleep, Dark Mode Toggle, Empty Recycle Bin, Screenshot
- **App launcher** with security-validated paths
- **Keyboard navigation**: `1-8` select wedges, `Esc` cancels, `Enter` confirms
- **DPI-aware** (Per-Monitor-V2) — tested at 125%, 150%, 200% scaling
- **Fully offline** — zero network calls, no telemetry, no auto-update
- **System tray** with config management and live reload

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 1903+ or Windows 11

### Build & Run

```bash
# Clone and build
git clone <repo-url>
cd WinRadial
dotnet build

# Run
dotnet run --project src/WinRadial
```

### Publish (Self-Contained Single File)

```bash
dotnet publish src/WinRadial -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output binary will be at:
```
src/WinRadial/bin/Release/net8.0-windows/win-x64/publish/WinRadial.exe
```

### Run Tests

```bash
dotnet test
```

## Usage

### Default Hotkey

Press **`Ctrl+Alt+Space`** to open the radial wheel at your cursor position.

### Navigation

| Input | Action |
|-------|--------|
| **Mouse hover** | Highlight wedge |
| **Left click** | Execute action or open submenu |
| **Click outside** | Close wheel |
| **`1`–`8`** | Select wedge by number |
| **`Left`/`Right`** arrow | Page through categories |
| **`Esc`** | Close wheel (or close submenu) |
| **`Enter`** | Confirm hovered selection |

### System Tray

WinRadial runs in the system tray. Right-click the tray icon for:
- **Open Config Folder** — opens `%APPDATA%\WinRadial\`
- **Reload Config** — live-reloads config without restarting
- **About** — version info
- **Exit** — clean shutdown

## Configuration

Configuration is stored at `%APPDATA%\WinRadial\config.json`. A default config is created on first run.

### Hotkey

```json
{
  "hotkey": {
    "modifiers": "Ctrl+Alt",
    "key": "Space"
  }
}
```

Valid modifiers: `Ctrl`, `Alt`, `Shift`, `Win` (combine with `+`).

### Categories & Actions

Each category has up to 8 action slots:

```json
{
  "categories": [
    {
      "name": "My Apps",
      "iconKey": "\uE74C",
      "slots": [
        {
          "actionId": "app_launch",
          "label": "VS Code",
          "iconKey": "\uE943",
          "path": "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe"
        }
      ]
    }
  ]
}
```

### Available Action IDs

| Action ID | Description | Requires `path` |
|-----------|-------------|:---:|
| `lock_screen` | Lock workstation | No |
| `sleep` | Put system to sleep | No |
| `toggle_dark_mode` | Toggle Windows dark/light mode | No |
| `empty_recycle_bin` | Empty the Recycle Bin | No |
| `screenshot` | Full-screen screenshot to Pictures | No |
| `app_launch` | Launch an application | **Yes** |
| `open_folder` | Open folder in Explorer | **Yes** |
| `separator` | Visual separator (no action) | No |

### Submenus

Add `children` to create a submenu ring:

```json
{
  "actionId": "app_launch",
  "label": "Browsers",
  "iconKey": "\uE774",
  "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
  "children": [
    { "actionId": "app_launch", "label": "Firefox", "path": "..." },
    { "actionId": "app_launch", "label": "Chrome", "path": "..." },
    { "actionId": "app_launch", "label": "Edge", "path": "..." }
  ]
}
```

### Appearance

Customize colors, radii, and opacity:

```json
{
  "appearance": {
    "innerRadius": 60,
    "outerRadius": 200,
    "subMenuRadius": 280,
    "backgroundColor": "#E61E1E2E",
    "hoverColor": "#CC6C63FF",
    "accentColor": "#FF7C73FF",
    "textColor": "#FFFFFFFF",
    "opacity": 0.95
  }
}
```

## Adding a New Action

1. Create a new class implementing `IWheelAction` in `src/WinRadial/Actions/`:

```csharp
public sealed class MyAction : IWheelAction
{
    public string Id => "my_action";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public MyAction(string label, string iconKey) { Label = label; IconKey = iconKey; }

    public Task ExecuteAsync()
    {
        // Your logic here
        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
```

2. Register it in `ActionRegistry.cs`:
```csharp
"my_action" => new MyAction(slot.Label ?? "Default", slot.IconKey ?? "\uE700"),
```

3. Add to `SecurityValidator.ValidActionIds`:
```csharp
"my_action",
```

4. Update `config.schema.json` enum to include `"my_action"`.

## Architecture

```
src/WinRadial/
├── Core/          # Infrastructure (hotkey, config, security, logging)
├── Actions/       # IWheelAction interface + built-in implementations
├── UI/            # WPF overlay (WheelWindow, WheelCanvas, WheelRenderer)
├── Tray/          # System tray management
└── Resources/     # Embedded config files
```

Key design decisions:
- **WheelRenderer** is pure math with zero WPF dependencies — fully unit-testable
- **Single hidden window** reused across activations for <100ms open latency
- **P/Invoke centralized** in `WindowInterop.cs` for auditability
- **Security-first**: all paths validated, no network calls, no elevation

## License

MIT
