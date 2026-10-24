# MouseYokeEnhanced

Decompiled source of **MouseYoke** (assembly `MouseYokeVJoy` 1.0.0), plus our improvements. It is a
.NET 8 WinForms x64 app that turns the mouse into a yoke for Microsoft Flight Simulator through the
**vJoy** virtual joystick: mouse → X/Y axes, keyboard rudder → Z axis, keys → vJoy buttons.

The user writes in Portuguese (pt-BR). The app UI and the manual are in English; keep them that way.

## Layout

- `MouseYoke/` — the **original shipped binaries** (exe, dll, pdb, `vJoyInterface.dll`, manual). Reference
  only: never edit. The build copies `vJoyInterface.dll` from here.
- `MouseYokeEnhanced/` — the source we work on (`MouseYokeVJoy.csproj`, code in `MouseYokeVJoy/`).
- `README.md` — the GitHub presentation page (logo, screenshots, highlights, quick start). It is
  **not** the manual: it links to `MouseYokeEnhanced/MouseYoke_Manual.md`, which is copied next to the
  exe. When a feature changes, update the manual, and the README if it mentions it. Both UTF-8.
- `docs/images/` — the README screenshots. Retake them after a visible UI change (see Build: a scratch
  copy with a demo profile; park the window away from the real cursor so no tooltip shows).
- `MouseYokeEnhanced/Assets/Brand/` — logo and icon PNGs (sources) and `MouseYoke.ico`, built from the
  1024 px icon by `make-icon.ps1`. The `.ico` is the exe icon (`ApplicationIcon`) and an embedded
  resource; rerun the script after changing the PNG.
- `.github/workflows/release.yml` — on every push to `main`, publishes `MouseYokeEnhanced/` as a new
  GitHub release `v<MAJOR>.<n>` (from v1.0; `MAJOR` is set in the workflow) with
  `MouseYokeEnhanced-v<MAJOR>.<n>.zip` and the commits since the last tag as notes. Inside the zip the
  folder is always `MouseYokeEnhanced\`, so unzipping a new version over an old one keeps its profiles. It stamps that version into `AssemblyInfo.cs` in CI only
  (the file keeps `1.0.0` in git). The original `MouseYoke/` binaries are never released; only
  `vJoyInterface.dll` comes along. The README links to the `releases/latest` page.
- `design/` — local, git-ignored mockups of the UI redesign (`redesign.html`). Never commit or publish it.

## Build

```powershell
dotnet build MouseYokeEnhanced\MouseYokeVJoy.csproj -c Release
```

- Needs the **.NET 10 SDK** (`LangVersion` is 14.0); the target is still `net8.0-windows7.0`, so running
  needs only the .NET 8 Desktop runtime.
- `nuget.config` at the root exists because this machine has no NuGet sources configured. Keep it.
- Output: `MouseYokeEnhanced/bin/Release/net8.0-windows7.0/` (ignored by git). Profiles, `overlay.cfg`
  (HUD position and size) and `settings.json` (app-wide settings) are written next to the exe at runtime.
- About 90 warnings are expected: MSB3245/MSB3243 (junk `HintPath` references left by the decompiler),
  CS0649 (P/Invoke structs) and CS8632 (`?` annotations without `<Nullable>`). CS8632 grows by one
  with every `(object? s, EventArgs e)` lambda, which is the house style. Don't chase them unless asked.
  Any warning or error of **another kind** is ours.

There are no tests. To check UI changes, build, launch the exe, open a tab by invoking its button
through UI Automation (`System.Windows.Automation`, `InvokePattern` on "Fly", "Feel", "Keys", "HUD"),
screenshot the window with `Graphics.CopyFromScreen`, then `CloseMainWindow()`. Owner-drawn controls
(`Segmented`, `StickMonitor`...) are found by their `Text`, which mirrors the accessible name; click a
segment with `PostMessage(WM_LBUTTONDOWN/UP)` on its handle so the real mouse doesn't move. To try a
profile without touching the real ones, copy the build output to a scratch folder and put test JSON
files in its `Profiles\`. Running the app installs a global keyboard hook, acquires vJoy device 1 and
stays on top. **Never activate flight (F9) in an automated run**: it hides the system cursor and
confines it to the primary monitor.

## Architecture

| File | Role |
|---|---|
| `MainForm.cs` | Logic: control loop, Raw Input, vJoy setup and checks, key handling, profile actions, HUD visibility |
| `MainForm.Ui.cs` | The screens, built in code (no designer): header, tabs Fly/Feel/Keys/HUD, status bar, compact window, tray, hotkey and button lists |
| `DarkControls.cs` | Owner-drawn controls: `Slider`, `ToggleSwitch`, `Segmented`, `TabButton`, `ActivateButton`, `StickMonitor`, `CurveView`, `LampStrip`, `KeyLegend`, `Stepper`, `HudPreview`, dark menus |
| `HudRenderer.cs` | Draws the four HUD styles (`HudStyle`) from a `HudState`; shared by the overlay, the Fly monitor and the HUD preview |
| `OverlayForm.cs` | Click-through HUD: per-pixel-alpha layered window (`UpdateLayeredWindow`), never activates; position mode with Done; notices |
| `Theme.cs` | Palette and fonts |
| `BrandIcon.cs` | Window and tray icons from the embedded `MouseYoke.ico`, and the "flying" variant with a red dot (set in `ToggleActive()`) |
| `AppSettings.cs` | App-wide settings in `<exe>\settings.json` (HUD style/opacity/options, on top, tray, compact) |
| `Profile.cs` / `ProfileManager.cs` | Per-aircraft settings, JSON files in `<exe>\Profiles\`, `_active.txt`; import/export, rename |
| `Hotkey.cs` | The list of rebindable keyboard commands (`Hotkey.All`) |
| `KeyboardHook.cs` | `WH_KEYBOARD_LL` hook; drops auto-repeat; `ReleaseStaleKeys()`; mouse buttons join it through `MouseButton()` |
| `CursorHider.cs` | Swaps the system cursors for a blank one; `Restore()` reloads the scheme |
| `VJoy.cs` | P/Invoke into `vJoyInterface.dll` (device id 1 is hard-coded) |

- **Threading:** the control loop (`Loop_Tick`, a WinForms `Timer` at 10 ms) and the hook callback both
  run on the UI thread. Keep hook handlers fast: a slow UI thread lags the whole system's keyboard.
- **Rates are per second, integrated over real `dt`** (a `Stopwatch`; the timer really runs at about 64 Hz).
  Trim and rudder use `rate × dt`; the spring decay and smoothing compound per-10 ms factors over `dt`.
  Never go back to per-tick constants.
- **Stick state:** `stickRoll/stickPitch` is the stick before trim. It holds during freelook (always, also in
  Spring) and in standby only with Hold stick position; otherwise `ResetStick()` zeroes it every standby tick.
  `PlaceCursor()` puts the cursor back on the held position (`lastInX/Y`, direct mode) on resume. Center
  stick also centers the rudder (`RudderValue`) but never touches the trims.
- **Flight screen:** on activation the primary monitor is fixed as `flightScreen`, the cursor is confined
  to it with `ClipCursor` (re-asserted every tick) and released on deactivate and on close.
- **Stuck keys:** `KeyboardHook.ReleaseStaleKeys()` runs every tick and fires the missing `KeyUp` for keys
  that are physically up (lost key-up after Win+L, UAC, an elevated window...).
- **Live UI:** every third tick the loop builds a `HudState`, updates the overlay and calls `RefreshLiveUi()`,
  which only touches the visible tab. The overlay re-renders only when its frame changes.
- **HUD visibility** is decided in one place, `UpdateOverlayVisibility()`: while flying, while being
  positioned, in standby if "Show in standby" is on, and briefly for a notice (`HudToast`).
- **Saving:** `SaveActiveProfile()` copies the UI into the profile at once and writes the file 300 ms
  later (`saveTimer`); `FlushSave()` writes now. Code that switches profiles writes the old one itself.
- `OverlayForm` takes the style in its constructor: `overlay.cfg` stores the top-left corner, and a later
  style change keeps the center, so creating it with the wrong size would move the HUD on every start.

## Hotkeys (per profile)

- The keys live on `Profile` (`ToggleVk`, `TrimUpVk`, `TrimDownVk`, `TrimLeftVk`, `TrimRightVk`,
  `TrimResetVk`, `RudderLeftVk`, `RudderRightVk`). `0` means unassigned. Defaults come from the
  property initializers, and `Hotkey.DefaultVk` reads them from a fresh `Profile`.
- **To add a command:**
  1. add an `int` property with a default initializer to `Profile`;
  2. add an entry to `Hotkey.All`;
  3. handle it in `MainForm.OnKeyDown`/`OnKeyUp`;
  4. if it should appear in "Keys in use" on the Fly tab, add it to `RefreshHotkeyUi()`.
- In key capture, Esc cancels and Backspace clears, so neither can be bound.
- Mouse buttons (right, middle, X1, X2 as VK 2/4/5/6) come from Raw Input in `WndProc` and go through the
  same `OnKeyDown`/`OnKeyUp`, so any command or vJoy button can use them. The primary button is left
  out (the user's decision). Raw Input can't swallow the click: it still reaches the sim.
- Conflicts among the hotkeys block the change (Swap/Cancel). A hotkey that is also a vJoy button only
  warns, on both lists (`HotkeyWarning`/`BindingWarning`): the command wins, except the rudder keys
  (while keyboard rudder is on), which press the button too. `CommandOn()` mirrors `OnKeyDown`; keep
  them in step.
- After any rebind or profile switch, call `ReleaseHeldKeys()`: key-ups are matched against the
  current bindings and would otherwise leave trim or rudder held.
- **Global hotkeys that work in every app are intentional** (the user's decision). Don't add a
  foreground-window filter unless asked.

## Conventions

- The code keeps the decompiler's style: tabs, file-scoped `namespace MouseYokeVJoy;`, explicit
  lambdas `(object? s, EventArgs e) => { ... }`, `nint`, ternaries in parentheses, no `Nullable`.
  Match the surrounding code; comments are rare and explain *why*.
- UI palette (dark) lives in `Theme`: background `18,18,22`; bars `28,28,36`; rows `30,30,38`;
  inputs/keys `45,45,55`; primary button `45,55,75`; blue `45,70,110`; danger `90,40,40`; accent
  `90,154,214`; amber (trim) `255,190,60`; green (stick) `120,255,140`; font Segoe UI 9. Use the
  `Theme` fields, not new literals.
- Build UI with the helpers in `MainForm.Ui.cs`: `NewColumn()` + `Scroller()` for a page, `Card()`,
  `AddRow()`, `AddSliderRow()` (caption, hint, value with unit, double-click resets), `Switch()`,
  `SwitchPair()`, `Header()`, `Styled()`/`ActionButton()`. A page column goes through `Scroller()`:
  AutoScroll on a TableLayoutPanel with Percent rows doesn't show a scrollbar reliably, and without it
  the last AutoSize row stretches to the bottom.
- Per-aircraft settings go on `Profile`; app-wide ones (HUD, window) on `AppSettings`.
- New `Profile`, `ButtonBinding` and `AppSettings` properties **must** have initializers: old JSON files
  without them must still load.
- Some magic numbers are leftovers from decompilation (`268435459u` = `RID_INPUT`, `48u/49u/50u` =
  HID X/Y/Z, `16384` = axis center). Named constants are welcome when you touch them.

## Known open issues (found in analysis, not fixed yet)

- The cursor stays invisible after a crash or kill (restored only on a normal exit).
- Profiles are stored next to the exe, so under Program Files nothing is saved (the status bar now
  shows "Not saved"), and a corrupt JSON is still silently replaced with defaults. Other
  `ProfileManager` IO errors are swallowed.
- Spring mode with a hidden cursor parks the cursor at the bottom-right pixel (risk of "Show desktop").
- Trim sign: Page Up makes `pitchTrim` negative (same as pushing the mouse forward, i.e. nose down),
  while the manual says "nose up". `InvertPitch` doesn't flip trim. Unresolved; ask before changing.
- The HUD and window sizes are in raw pixels; at high DPI they look small (the HUD has its Size slider).

## Git

- Remote: `origin` → github.com/rangelsantos/MouseYokeEnhanced, branch `main`.
- Commit or push only when the user asks.
- This is decompiled third-party code; licensing and redistribution haven't been confirmed.
