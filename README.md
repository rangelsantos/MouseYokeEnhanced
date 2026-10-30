<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-2b2b33?logo=windows" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/.NET-8%20Desktop-5a9ad6?logo=dotnet" alt=".NET 8 Desktop">
  <img src="https://img.shields.io/badge/needs-vJoy-5a9ad6" alt="Needs vJoy">
  <a href="https://ko-fi.com/rangelsantosss"><img src="https://img.shields.io/badge/buy%20me%20a%20coffee-%E2%98%95-ffbe3c?logo=kofi" alt="Buy me a coffee"></a>
</p>

<p align="center">
  <img src="MouseYokeEnhanced/Assets/Brand/mouseyoke-enhanced-logo-512.png" width="240" alt="MouseYokeEnhanced">
</p>
<h3 align="center">Fly Microsoft Flight Simulator with your mouse — no joystick needed.</h3>

<p align="center">
  <img src="docs/images/fly.png" width="380" alt="MouseYokeEnhanced, Fly tab">
</p>

MouseYokeEnhanced turns your mouse into the aircraft's control stick. Move the mouse and the
plane rolls and pitches; hold a key to trim or work the rudder. It talks to the sim through
**vJoy**, a free virtual joystick, so Flight Simulator sees it as an ordinary controller.

It's made for people without a joystick or yoke who still want smooth, precise control,
especially on takeoff and landing.

---

## Highlights

- **Two stick modes.** *Direct* maps the mouse position to the stick. *Spring* behaves like a
  spring-loaded sidestick: nudge it and it re-centers on its own.
- **Tune the feel.** Sensitivity per axis, expo, deadzone, smoothing and auto-center, with a live
  response curve that shows what each slider does.
- **Trim that holds.** Pitch and roll trim on hold-to-move keys, plus a one-key reset.
- **No jumps.** Hold a freelook key or mouse button to look around while the stick stays put,
  center the stick and rudder with one key, and optionally keep the stick's position when you
  start or stop flying.
- **Keyboard rudder.** Smooth rudder on the vJoy Z axis from two keys.
- **Extra buttons.** Map any key to a vJoy button (gear, flaps, brakes, lights…) and give it a
  name. You get as many as vJoy is set up for; 16 is recommended.
- **Every key is yours.** Rebind or clear any command, on a key or a mouse button. Conflicts are
  caught, with a one-click swap.
- **On-screen HUD.** Four styles drawn over the sim, click-through, with size, opacity, background and position.
- **Profiles per aircraft.** Each keeps its own feel, keys and buttons. Import and export them to
  share.
- **Stays out of the way.** Compact window, close to tray, and the icon shows a red dot while
  you're flying.
- **Safe by design.** While flying, the pointer is held on the main screen so it can't slip onto
  another monitor, and keys whose release Windows lost (after Win+L or a UAC prompt) are let go
  automatically.

---

## A quick tour

### Fly — what the sim is receiving

The Fly tab, shown at the top of this page, is the live view. The stick picture shows the output live: the green cross is the stick, the amber arrows are pitch
trim (right) and roll trim (bottom), and the bar underneath is the rudder. The vJoy button lamps
light up while their keys are down, and **Keys in use** lists your current bindings.

Press **F9** or click **Activate** to start flying with the mouse. Press **F9** again to get the
pointer back for the cockpit.

The chip at the top right checks vJoy for you: green **vJoy ready** means all set; click it when
it's amber or red to see what's missing. The **⚙** menu next to it holds the window options
(always on top, close to tray, compact window) and **About**, with the version, the credits and
links to the repository, its releases and the manual.

### Stick — make it respond your way

<img src="docs/images/stick.png" width="360" align="right" alt="Stick tab">

Pick **Direct** or **Spring** at the top, and turn on **Hold stick position** if the stick should
stay where it is when you start or stop flying. Then adjust the **Response** sliders and invert an
axis if needed. The graph draws the curve against a dashed linear line, and while you fly a green
dot shows where the mouse sits on it.

Below, only the sliders of the chosen mode are shown (deadzone and smoothing for Direct,
auto-center for Spring), and **Trim & rudder** sets the trim speed and turns keyboard rudder on.
Double-click any slider to put it back to its starting value.

<br clear="right">

### Keys — commands and vJoy buttons

<p>
  <img src="docs/images/keys-commands.png" width="49%" alt="Commands list">
  <img src="docs/images/keys-buttons.png" width="49%" alt="vJoy buttons list">
</p>

**Commands** holds every command: fly/standby, freelook, center stick, trim, trim reset and
rudder. Click a key and press the new one, or a mouse button (right, middle, 4 or 5). **Esc** cancels, **Backspace** clears, **↺** restores the default.

**vJoy buttons** maps keys to joystick buttons. Click **+ Add key**, press the key, choose the
button number and name it. Then bind that button in the sim's controls.

### HUD — your stick, over the sim

<img src="docs/images/hud.png" width="360" align="right" alt="HUD tab">

A small click-through display drawn over Flight Simulator: stick position, trim, rudder and
whether you're **ACTIVE** or in **STANDBY**. Choose the style, size and opacity, then drag it
wherever you like with **Position HUD…**. Rudder and trim indicators only appear when the
profile uses them, or you can show or hide each one yourself. Turn the background down to 0%
for marks only (outlined so they read over a bright sky), and hide the status line for a
cleaner HUD.

It also flashes **ACTIVE**, **STANDBY**, **TRIM RESET** or **CENTER** when you press the keys, so you know
it worked even with the window hidden behind the sim.

<br clear="right">

| Sidestick | Minimal |
|:---:|:---:|
| <img src="docs/images/hud-sidestick.png" alt="Sidestick HUD"> | <img src="docs/images/hud-minimal.png" alt="Minimal HUD"> |
| **Strip** | **Classic** |
| <img src="docs/images/hud-strip.png" alt="Strip HUD"> | <img src="docs/images/hud-classic.png" alt="Classic HUD"> |

### Compact window and tray

<img src="docs/images/compact.png" width="420" alt="Compact window">

Shrink MouseYokeEnhanced to a strip with just the essentials (**⚙ → Compact window**), for a second screen, or drag the full window
shorter on a small screen (the tabs scroll and the height is remembered). With **Close to tray**,
closing the window keeps it running with your keys working. Right-click the tray icon to show
the window, start or stop flying, or exit. A red dot on the icon, in the tray and on the taskbar,
means you're flying.

---

## Default keys

| Key | Action |
|---|---|
| **F9** | Start / stop mouse flying |
| **Page Up / Page Down** | Pitch trim (hold) |
| **Insert / Delete** | Roll trim (hold) |
| **Home** | Reset trim |
| **Comma , / Period .** | Rudder left / right (hold, with keyboard rudder on) |
| *not set* | Freelook (hold) |
| *not set* | Center stick and rudder |

All of them can be changed on the **Keys** tab, per profile.

---

## Getting started

1. **Install vJoy** from [github.com/jshafer817/vJoy/releases](https://github.com/jshafer817/vJoy/releases)
   and restart if asked.
2. **Configure vJoy** (once): open *Configure vJoy*, select **Device 1**, tick **X**, **Y** and
   **Z**, set **16** buttons, and apply.
3. **Start MouseYokeEnhanced** (`MouseYokeEnhanced.exe`). The status bar should say **Ready. F9 to fly.**
4. **Bind the axes in the sim** (once): *Options → Controls & Sensitivity*, pick **vJoy Device**,
   and assign **Ailerons** (mouse left/right), **Elevator** (mouse up/down) and, if you use keyboard
   rudder, **Rudder** (hold a rudder key).

Run the sim in **windowed** or **borderless** mode, and don't run it as Administrator, or
MouseYokeEnhanced can't see your keys.

📖 The full **[user manual](MouseYokeEnhanced/MouseYokeEnhanced_Manual.md)** covers every option and has a
*Quick fixes* section.

## Requirements

- Windows 10 or 11, 64-bit
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
- [vJoy](https://github.com/jshafer817/vJoy/releases), device 1 with X, Y, Z and 16 buttons

## Credits

MouseYokeEnhanced is created by **Rangel Santos** and published here on GitHub. It builds on the
original **MouseYoke** (MouseYokeVJoy 1.0.0) by **bbnycreamtea** ([flightsim.to](https://flightsim.to)),
adding a new interface and new features. Thanks to bbnycreamtea for the original app. Joystick
output goes through [vJoy](https://github.com/jshafer817/vJoy).

If MouseYokeEnhanced makes your flights better, you can
[buy me a coffee](https://ko-fi.com/rangelsantosss) ☕ on Ko-fi or [sponsor it](https://github.com/sponsors/rangelsantos) on GitHub.

<p align="center">Enjoy the skies! ✈️</p>
