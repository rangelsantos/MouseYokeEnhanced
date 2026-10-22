# MouseYokeEnhanced

Decompiled source of **MouseYoke** (assembly `MouseYokeVJoy` 1.0.0), plus our improvements. It is a
.NET 8 WinForms x64 app that turns the mouse into a yoke for Microsoft Flight Simulator through the
**vJoy** virtual joystick: mouse → X/Y axes, keyboard rudder → Z axis, keys → vJoy buttons.

The user writes in Portuguese (pt-BR). The app UI and the manual are in English; keep them that way.

## Layout

- `MouseYoke/` — the **original shipped binaries** (exe, dll, pdb, `vJoyInterface.dll`, manual). Reference
  only: never edit. The build copies `vJoyInterface.dll` from here.
- `MouseYokeEnhanced/` — the source we work on (`MouseYokeVJoy.csproj`, code in `MouseYokeVJoy/`).
- `README.md` — an **exact copy** of `MouseYokeEnhanced/MouseYoke_Manual.md`. When you change the manual,
  copy it over (`cp MouseYokeEnhanced/MouseYoke_Manual.md README.md`). Both must stay UTF-8.

## Build

```powershell
dotnet build MouseYokeEnhanced\MouseYokeVJoy.csproj -c Release
```

- Needs the **.NET 10 SDK** (`LangVersion` is 14.0); the target is still `net8.0-windows7.0`, so running
  needs only the .NET 8 Desktop runtime.
- `nuget.config` at the root exists because this machine has no NuGet sources configured. Keep it.
- Output: `MouseYokeEnhanced/bin/Release/net8.0-windows7.0/` (ignored by git). Profiles and
  `overlay.cfg` are written next to the exe at runtime.
- About 55 warnings are expected and predate our work: MSB3245/MSB3243 (junk `HintPath` references
  left by the decompiler), CS8632 (`?` annotations without `<Nullable>`), CS0649 (P/Invoke structs).
  Don't chase them unless asked. Any **new** warning or error is ours.

There are no tests. To check UI changes, build, launch the exe, select the tab through UI Automation
(`System.Windows.Automation`, `SelectionItemPattern`/`InvokePattern`), screenshot the window with
`Graphics.CopyFromScreen`, then `CloseMainWindow()`. Running the app installs a global keyboard hook,
acquires vJoy device 1 and stays on top. **Never activate flight (F9) in an automated run**: it hides
the system cursor and confines it to the primary monitor.

## Architecture

| File | Role |
|---|---|
| `MainForm.cs` | Almost everything: UI built in code (no designer), control loop, Raw Input, hotkeys, profile CRUD |
| `OverlayForm.cs` | Click-through HUD (layered + transparent window, GDI+) |
| `Profile.cs` / `ProfileManager.cs` | Per-aircraft settings, JSON files in `<exe>\Profiles\`, `_active.txt` |
| `Hotkey.cs` | The list of rebindable keyboard commands (`Hotkey.All`) |
| `KeyboardHook.cs` | `WH_KEYBOARD_LL` hook; drops auto-repeat; `ReleaseStaleKeys()` |
| `CursorHider.cs` | Swaps the system cursors for a blank one; `Restore()` reloads the scheme |
| `VJoy.cs` | P/Invoke into `vJoyInterface.dll` (device id 1 is hard-coded) |

- **Threading:** the control loop (`Loop_Tick`, a WinForms `Timer` at 10 ms) and the hook callback both
  run on the UI thread. Keep hook handlers fast: a slow UI thread lags the whole system's keyboard.
- **Rates are per second, integrated over real `dt`** (a `Stopwatch`; the timer really runs at about 64 Hz).
  Trim and rudder use `rate × dt`; the spring decay and smoothing compound per-10 ms factors over `dt`.
  Never go back to per-tick constants.
- **Flight screen:** on activation the primary monitor is fixed as `flightScreen`, the cursor is confined
  to it with `ClipCursor` (re-asserted every tick) and released on deactivate and on close.
- **Stuck keys:** `KeyboardHook.ReleaseStaleKeys()` runs every tick and fires the missing `KeyUp` for keys
  that are physically up (lost key-up after Win+L, UAC, an elevated window...).

## Hotkeys (per profile)

- The keys live on `Profile` (`ToggleVk`, `TrimUpVk`, `TrimDownVk`, `TrimLeftVk`, `TrimRightVk`,
  `TrimResetVk`, `RudderLeftVk`, `RudderRightVk`). `0` means unassigned. Defaults come from the
  property initializers, and `Hotkey.DefaultVk` reads them from a fresh `Profile`.
- **To add a command:**
  1. add an `int` property with a default initializer to `Profile`;
  2. add an entry to `Hotkey.All`;
  3. handle it in `MainForm.OnKeyDown`/`OnKeyUp`;
  4. if it should appear in the Flight tab legend, add it to `RefreshHotkeyUi()`.
- In key capture, Esc cancels and Backspace clears, so neither can be bound.
- Conflicts are checked only among the hotkeys (Swap/Cancel), not against vJoy button bindings.
- After any rebind or profile switch, call `ReleaseHeldKeys()`: key-ups are matched against the
  current bindings and would otherwise leave trim or rudder held.
- **Global hotkeys that work in every app are intentional** (the user's decision). Don't add a
  foreground-window filter unless asked.

## Conventions

- The code keeps the decompiler's style: tabs, file-scoped `namespace MouseYokeVJoy;`, explicit
  lambdas `(object? s, EventArgs e) => { ... }`, `nint`, ternaries in parentheses, no `Nullable`.
  Match the surrounding code; comments are rare and explain *why*.
- UI palette (dark): background `18,18,22`; bars `28,28,36`; rows `30,30,38`; inputs/keys `45,45,55`;
  primary button `45,55,75`; blue `45,70,110`; danger `90,40,40`; font Segoe UI 9.
  Use `Styled()`, `AddRow()` and `AddSliderRow()` when building UI.
- New `Profile` properties **must** have initializers: old JSON files without them must still load.
- Some magic numbers are leftovers from decompilation (`524448` = `WS_EX_LAYERED|TOOLWINDOW|TRANSPARENT`,
  `268435459u` = `RID_INPUT`, `48u/49u/50u` = HID X/Y/Z). Named constants are welcome when you touch them.

## Known open issues (found in analysis, not fixed yet)

- The cursor stays invisible after a crash or kill (restored only on a normal exit).
- A missing `vJoyInterface.dll` crashes the app on startup without a friendly message.
- `ProfileManager` swallows every IO error. Profiles are stored next to the exe, so nothing is saved
  under Program Files, and a corrupt JSON is silently replaced with defaults. Every slider change
  writes to disk synchronously.
- `OverlayForm` doesn't set `ShowWithoutActivation`, so it may steal focus from the sim.
- Spring mode with a hidden cursor parks the cursor at the bottom-right pixel (risk of "Show desktop").
- `GetVJDAxisExist` is never used: a missing Z axis or missing buttons fail silently.
- Deadzone and Smoothing have no effect in spring mode (the manual doesn't say so).
- Trim sign: Page Up makes `pitchTrim` negative (same as pushing the mouse forward, i.e. nose down),
  while the manual says "nose up". `InvertPitch` doesn't flip trim. Unresolved; ask before changing.
- A case-only rename ("cessna" → "Cessna") becomes "Cessna 2". Disabling keyboard rudder while it is
  deflected leaves Z stuck at that value.

## Git

- Remote: `origin` → github.com/rangelsantos/MouseYokeEnhanced, branch `main`.
- Commit or push only when the user asks.
- This is decompiled third-party code; licensing and redistribution haven't been confirmed.
