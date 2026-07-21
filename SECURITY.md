# Security Policy — WinRadial

## Threat Model

WinRadial is a **local-only, standard-user Windows desktop application**. It has no network access, no cloud dependencies, and never requests elevated privileges.

### Trust Boundaries

| Boundary | Description |
|----------|-------------|
| **User config** | `%APPDATA%\WinRadial\config.json` — user-writable, validated before use |
| **System APIs** | P/Invoke calls to Windows APIs (hotkey, screen capture, registry) |
| **Process launch** | Starting external applications via `Process.Start` |
| **Registry** | Dark mode toggle reads/writes HKCU only |

### Assets Protected

| Asset | Protection |
|-------|------------|
| Config integrity | JSON schema validation; falls back to built-in defaults on failure |
| Process execution | Path validation prevents traversal and injection |
| Registry scope | HKCU only, read-before-write, try/catch wrapped |
| System stability | Global exception handlers prevent silent crashes |

## Security Controls

### 1. Zero Network Access

WinRadial makes **zero network calls**. There is no `HttpClient`, `System.Net`, `WebClient`, or any networking code in the application. This eliminates:
- Data exfiltration
- Update hijacking
- Remote code execution via network
- DNS-based attacks

### 2. No Self-Elevation

The application manifest specifies `requestedExecutionLevel = asInvoker`. WinRadial **never requests administrator privileges** and runs at standard user integrity level.

### 3. Path Validation (`SecurityValidator`)

All file paths (for `app_launch` and `open_folder` actions) are validated before use:

- **Resolved** with `Path.GetFullPath()` to canonicalize
- **Directory traversal rejected**: any path containing `..` is blocked
- **Shell metacharacters rejected**: characters `| & ; < > ` $ ! { } ( ) [ ] " '` and control characters are blocked
- **Existence verified**: `File.Exists()` or `Directory.Exists()` must return true

### 4. Safe Process Launching

- `Process.Start` uses `ProcessStartInfo.ArgumentList` — **never concatenated command strings**
- Arguments are split respecting quoted segments, preventing injection
- `UseShellExecute = true` delegates to Windows shell for proper file association handling

### 5. Action ID Whitelist

Config `actionId` values are validated against a fixed `HashSet<string>` of registered actions:
```
lock_screen, sleep, toggle_dark_mode, empty_recycle_bin, 
screenshot, app_launch, open_folder, separator
```

Unknown action IDs are rejected at config load time. No dynamic code loading, reflection-based instantiation, or plugin DLLs.

### 6. Config Validation & Fallback

- Config is validated against business rules (required fields, types, bounds, action ID whitelist)
- JSON Schema (`config.schema.json`) defines the contract
- On **any** validation failure:
  1. Errors logged to `%APPDATA%\WinRadial\logs\`
  2. Falls back to safe embedded default config
  3. **Never executes unvalidated fields**

### 7. Registry Access (Dark Mode Toggle)

- **Scope**: HKCU only (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`)
- **Pattern**: Read current value → toggle → write new value
- **Error handling**: Entire operation wrapped in try/catch
- **No HKLM writes**: Never touches machine-wide registry

### 8. Icon Sources

Icons are loaded from:
- **Segoe MDL2 Assets font** — built into Windows 10/11 (glyph mapping)
- **SHGetFileInfo** — extracts icons from local executable files only
- **Never remote sources** — no URL-based icon loading

### 9. Resource Cleanup

- Global hotkey unregistered via `IDisposable` pattern on exit
- Per-render WPF brushes/pens disposed to avoid GDI handle leaks
- System tray icon removed via `Shell_NotifyIcon(NIM_DELETE)` on shutdown
- Named `Mutex` released on exit

### 10. Error Handling

- `AppDomain.UnhandledException` — logs and shows user message
- `DispatcherUnhandledException` — logs, marks handled (prevents crash)
- `TaskScheduler.UnobservedTaskException` — logs, marks observed
- All logged to `%APPDATA%\WinRadial\logs\` with 7-day auto-prune

## Out of Scope

The following are explicitly **not included** in WinRadial v1:

| Feature | Reason |
|---------|--------|
| Plugin/DLL loading | Eliminates code injection vector |
| Auto-update | Eliminates update hijacking |
| Cloud sync | Eliminates data exfiltration |
| Remote icon URLs | Eliminates SSRF/content injection |
| Admin elevation | Reduces attack surface |
| Arbitrary command execution | Only whitelisted actions supported |

## Reporting Vulnerabilities

If you discover a security issue, please report it responsibly by opening a private issue or contacting the maintainers directly. Do not disclose vulnerabilities publicly until a fix is available.

## Audit Checklist

- [ ] All P/Invoke declarations in `Core/WindowInterop.cs` — review for buffer overflows
- [ ] `SecurityValidator.ValidatePath()` — verify regex covers all metacharacters
- [ ] `ActionRegistry.Create()` — verify no dynamic loading or reflection
- [ ] `ConfigService.Validate()` — verify action ID whitelist is enforced
- [ ] `ToggleDarkModeAction` — verify HKCU-only registry scope
- [ ] `AppLaunchAction.ExecuteAsync()` — verify ArgumentList usage
- [ ] `app.manifest` — verify `asInvoker` execution level
- [ ] Full codebase — verify zero `HttpClient`/`System.Net` usage
