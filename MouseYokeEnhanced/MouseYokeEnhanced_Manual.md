# MouseYokeEnhanced — Fly with your mouse

**Fly Microsoft Flight Simulator using just your mouse and keyboard — no joystick needed.**

MouseYoke turns your mouse into the plane's control stick: move it to roll and pitch, and
the plane responds. It's made for people who don't have a joystick or yoke but still want
smooth, enjoyable control — especially during takeoff and landing.

---

## What you need

1. **This app** (the folder you downloaded).
2. **vJoy** — a small free program that lets the app talk to Flight Simulator. One-time setup, explained below.
3. **Flight Simulator** running in **windowed** or **borderless** mode (not exclusive fullscreen).

That's it.

---

## First-time setup (about 5 minutes)

### Step 1 — Install vJoy

1. Download vJoy from: **https://github.com/jshafer817/vJoy/releases** (get the file ending in `.exe`).
2. Run it, click through the installer, and **restart your PC** if it asks.

### Step 2 — Set up vJoy (do this once)

1. Open your Start menu and search for **Configure vJoy**. Open it.
2. On the left, make sure **Device 1** is selected.
3. Tick the boxes for **X**, **Y**, and **Z**.
4. Find **Number of Buttons** and set it to **16**.
5. Click **Apply**, then **OK**. You can close this — you won't need it again.

### Step 3 — Start MouseYoke

Double-click **MouseYokeVJoy** in the folder.

- If Windows shows a blue "Windows protected your PC" box, click **More info**, then **Run anyway**. (This just happens with small apps that aren't from big companies — it's safe.)
- If it says it needs ".NET," Windows will point you to a quick free download from Microsoft. Install it, then start MouseYoke again.

The app should say **"Ready. F9 to fly."** in the status bar at the bottom.

The chip at the top right shows how vJoy is set up, for example **vJoy 1 · XYZ · 16** (device 1,
axes X, Y and Z, 16 buttons). Green means all good. If it turns amber or red, click it: it tells
you what's missing and how to fix it in Configure vJoy.

The **ⓘ** button next to it opens **About**: the version you have, the credits, and links to the
GitHub repository, its Releases page and this manual. **Copy info** copies your version and vJoy setup,
ready to paste into a bug report.

- If it says **vJoyInterface.dll is missing**, put that file back in the MouseYoke folder (it
  comes with the app).

### Step 4 — Tell Flight Simulator about it (do this once)

1. Start Flight Simulator.
2. Go to **Options → Controls & Sensitivity**.
3. At the top, in the device list, choose **vJoy Device**.
4. Set these three:
   - **Ailerons Axis** → move your mouse **left/right** when it asks for input.
   - **Elevator Axis** → move your mouse **up/down**.
   - **Rudder Axis** → (only if you'll use keyboard rudder — see below) hold your rudder key.
5. Save. You're done setting up.

> Tip: if a control moves the wrong way, tick **Invert pitch** or **Invert roll** on the **Feel** tab, or use the sim's own "reverse" switch for that axis.

---

## How to fly

1. Keep MouseYoke open (on a second screen, or just behind the sim).
2. Press **F9** (or click the big **Activate** button) to start flying with the mouse. Press **F9**
   again to stop (handy when you need to click buttons in the cockpit).

The window has four tabs:

- **Fly** — what's happening right now: a live picture of the stick, the trim and the rudder,
  lights for the vJoy buttons, and the keys you're using.
- **Feel** — how the stick responds: the stick mode and the sliders.
- **Keys** — every key: the commands (trim, rudder…) and the keys you mapped to vJoy buttons.
- **HUD** — the on-screen display, plus a few window options.

On a small screen, drag the top or bottom edge of the window to make it shorter (down to half of
its tallest size); the tabs scroll. MouseYoke remembers the height for next time.

While you're flying, the pointer stays on your main screen, so it can't slip onto another
monitor and throw the controls to one side. Press **F9** to stop, and it's free again.

### Your keys

These are the starting keys. You can change any of them (see **Changing your keys** below).

- **F9** — start / stop mouse flying
- **Page Up / Page Down** — trim the nose up or down (hold)
- **Insert / Delete** — trim left or right (hold)
- **Home** — reset trim to neutral
- **Comma , / Period .** — rudder left or right (hold; only when keyboard rudder is on)

Two more commands start with no key; give them one on the Keys tab if you want them:

- **Freelook (hold)** — while you hold it, the mouse is yours again (to look around) and the
  stick stays exactly where it was. Let go and you fly on from the same spot. A mouse button
  works well here; **Mouse right** pairs with the sim's own right-click camera look.
- **Center stick** — puts the stick and the rudder back in the middle. Your trim stays as it is.

Think of **trim** as "hold this attitude for me." Set your climb or descent, trim it in, and
the plane holds it so you're not fighting the controls.

The **Fly** tab always shows the keys you're using right now, under **Keys in use**.

On the Fly tab, the stick picture shows what the sim receives: the green cross is the stick, the
amber arrows are the pitch trim (right side) and roll trim (bottom), and the bar underneath is the
rudder.

---

## Changing your keys

Open the **Keys** tab and pick **Commands**. Every keyboard command is listed there.

1. Click the key next to a command. It changes to **Press a key…**
2. Press the key you want to use. A mouse button works too: right, middle, or the two side
   buttons (**Mouse 4** and **Mouse 5**). The left button can't be used, since it clicks.

- **Esc** cancels without changing anything.
- **Backspace** leaves the command with no key ("Not set").
- The **×** button next to a command removes its key, which turns the command off. Click the
  key again to give it a new one.
- The **↺** button next to a command puts back its starting key.
- **Restore defaults** puts back all the starting keys at once.

If the key you press is already used by another command, the app tells you which one and
asks what to do: **Swap** gives the other command your old key, **Cancel** keeps things as
they were.

If a command key is also mapped to a vJoy button, both lists show a warning saying what will
happen (usually the command wins and the button won't fire; for the rudder keys, both happen).

Your keys are saved with the aircraft profile, so each plane can have its own.

---

## Two ways to fly — pick what feels best

At the top of the **Feel** tab you choose the **stick mode**:

- **Direct (default):** the mouse's position on screen is the control position. Precise, but
  you have to bring the mouse back to the middle yourself.
- **Spring:** it feels like a real spring-loaded sidestick — nudge to turn or climb, and it
  springs back to center on its own. Most people find this **much** easier for takeoff and
  landing. Turn on **Hide cursor** too (see below) for the best experience.

Try both and keep whichever you like. You can set it differently for each aircraft (see Profiles).

Below the modes, **Hold stick position** keeps the stick where it is when you press F9, both
ways. Stop flying in a turn and the sim keeps getting that same turn; start again and the stick
picks up from there, with the pointer put back where it was. Without it, the stick goes to the
middle when you stop and starts from the middle when you fly again. Either way, **Center stick**
brings it back to the middle whenever you like.

---

## Making it feel just right

On the **Feel** tab you'll find some sliders, each with its value next to it. You don't need to
understand them all — just nudge and see what feels good. **Double-click** a slider to put it
back to its starting value; the arrow keys move it one step at a time. Rough starting points:

- **Sensitivity** — higher = more responsive, lower = gentler. Big airliners like it gentle.
- **Expo** — makes small movements gentle and big movements strong. Great for smooth landings. Try 40–50.
- **Deadzone** — (Direct mode only) a small "dead" spot in the middle so the plane sits still when you're not moving.
- **Smoothing** — (Direct mode only) smooths out shaky input.
- **Auto-center** — (Spring mode only) how quickly the stick springs back to center.
- **Trim rate** — how fast the trim moves while you hold a trim key.

Sliders that don't apply to the mode you picked are grayed out. The **Response** graph above
them shows what Expo and Deadzone do: the dashed line is a plain, linear response, and while
you're flying a green dot shows where your mouse is on the curve.

---

## Handy extras

- **Hide cursor** — (switch on the **Fly** tab) hides the mouse pointer while flying so it's not
  floating in your view. Recommended, especially in spring mode. It comes back the moment you
  press F9 to stop.
- **Rudder on your keyboard** — on the **Keys** tab, under **Commands → Rudder**, turn on
  **Keyboard rudder** and you get smooth rudder without a joystick. The rudder keys
  (**Comma ,** and **Period .** to start with) and the rudder speed are right there too.
  (Remember to bind Rudder Axis in the sim, Step 4 above.)
- **Freelook** — give **Freelook (hold)** a key or mouse button on the Keys tab. Hold it to take
  your mouse off the stick for a moment: the stick and the trim stay put, the pointer stays
  hidden, and the HUD shows **FREELOOK**. In Spring mode the stick doesn't spring back while
  you hold it. Clicks still reach the sim, so a button that does something there (like the
  right button, which turns the camera) does both.
- **Extra buttons** — on the **Keys** tab, pick **vJoy buttons**. Click **+ Add key**, press the
  key (or a mouse button), and choose the button number with the arrows; then bind that "button" in the sim's
  controls (gear, flaps, brakes, autopilot…). You can give each one a name, like "Landing gear",
  so you remember what it does. The light on the left turns green while the key is down.

---

## The on-screen HUD

The HUD is a small display drawn over the sim while you're flying. It shows the same thing as
the stick picture on the Fly tab: where the stick is, the pitch and roll trim, the rudder, and
whether you're flying (**ACTIVE**) or not (**STANDBY**). Everything is on the **HUD** tab:

- **Style** — **Sidestick** (the default: corner brackets for full travel, a green cross for the
  stick), **Minimal** (just the brackets and the cross), **Strip** (a thin bar for the edge of
  the screen) or **Classic** (the original round display, with a rudder ball underneath, like a
  turn coordinator's). The preview at the top shows it.
- **Size** (50–250%) and **Opacity**.
- **Elements** — the rudder, the pitch trim and the roll trim each have **Auto**, **Show** or
  **Hide**. **Auto** (the default) shows an element only when this aircraft uses it: the rudder
  when **Keyboard rudder** is on, a trim when it has a key. A trim that is still applied stays on
  the HUD even after you remove its keys. The HUD closes up around what's hidden, so it gets
  smaller. **Show** always draws it (the rudder says **OFF** when it isn't on); **Hide** never
  does. The line under each name says what Auto is doing right now. The stick picture on the Fly
  tab always shows everything.
- **Show HUD** — turn it off if you don't want a HUD at all (also on the Fly tab).
- **Show numbers** — the trim and rudder values under the Sidestick HUD.
- **Fade when centered** — the HUD dims while the stick is at rest, so it stays out of the way.
- **Show in standby** — keep the HUD on screen (grayed) when you're not flying.
- **Position HUD…** — shows the HUD so you can drag it anywhere. Click **Done** (on the HUD or
  in the app) when it's in place. **Reset position** puts it back at the top of your main screen.

When you press F9, reset the trim or center the stick, the HUD briefly shows **ACTIVE**,
**STANDBY**, **TRIM RESET** or **CENTER**, so you know it worked even when the MouseYoke window is hidden behind the sim.

The HUD settings are the same for every aircraft profile.

---

## Window options

At the bottom of the **HUD** tab:

- **Always on top** — keeps the MouseYoke window above other windows.
- **Close to tray** — closing the window keeps MouseYoke running (your keys keep working) with
  an icon next to the clock. Right-click the icon to show the window, start or stop flying, or
  exit. While you're flying, the icon (next to the clock and on the taskbar) shows a small red
  dot.
- **Compact window** — shrinks MouseYoke to a small strip with just the essentials (flying or
  standby, the profile, the stick and the trim). Click **Expand** to get the full window back.

---

## Aircraft profiles

Fly more than one plane? You can save a **profile per aircraft** — each remembers its own feel,
its own extra buttons and its own keys.

Click the **Aircraft** button at the top to switch profiles. The **⋯** button next to it has the
rest:

- **New profile** — start a fresh profile.
- **Duplicate** — copy the current one as a starting point.
- **Rename** — give it a new name.
- **Import / Export** — load a profile from a file, or save the current one to a file to keep a
  copy or share it with a friend.
- **Open profiles folder** — where the profile files live.
- **Delete** — removes the current profile (you'll be asked first).

Everything saves automatically ("Saved" at the bottom right). Just pick your plane's profile
before you fly.

---

## Updating to a new version

New versions (v1.0, v1.1…) are on the
**[Releases page](https://github.com/rangelsantos/MouseYokeEnhanced/releases/latest)**. Download
`MouseYokeEnhanced-v<version>.zip`, close MouseYoke (right-click the tray icon → Exit if it's in
the tray), and unzip it over your old **MouseYokeEnhanced** folder, replacing the files. Your
profiles and settings are kept: they live in that folder and the zip doesn't include any. To see
which version you have, click **ⓘ** at the top right; **Releases** there opens the
Releases page.

---

## Quick fixes

- **The plane doesn't move in the sim.** Make sure MouseYoke is open and you pressed **F9**.
  Double-check you set the axes in Step 4, and that the sim is in windowed or borderless mode.
- **My keys don't do anything.** Don't run Flight Simulator "as Administrator" — that stops
  the app from seeing your keys. Just start it normally. Also check the **Keys** tab: make
  sure the command has a key (not "Not set") and that you picked the right aircraft profile.
- **A key does two things at once, or a button never fires.** If the same key is a command and
  an extra button, the Keys tab shows a warning on both. The command wins (for the rudder keys,
  both happen). Give one of them a different key.
- **The pointer won't go to my other screen.** That's on purpose while you're flying. Press
  **F9** to stop, and the pointer can go anywhere again.
- **The rudder won't set up in the sim.** The rudder only appears while it's moving, so
  **hold** your rudder key at the moment the sim asks you to move an axis. Also make sure you
  ticked **Z** in vJoy (Step 2; the vJoy chip at the top shows **XYZ** when it's on) and turned on
  **Keyboard rudder** on the Keys tab.
- **"Not saved" at the bottom right.** MouseYoke couldn't write your profile. Move the
  MouseYoke folder somewhere you can write to (for example Documents, not Program Files).
- **The pointer got stuck invisible after a crash.** Just start MouseYoke again and press
  F9 on and off, or lock and unlock your PC (Windows key + L). Your cursor comes right back.

---

## Credits

MouseYokeEnhanced is created by **Rangel Santos** and published on
[GitHub](https://github.com/rangelsantos/MouseYokeEnhanced). It is based on the original
**MouseYoke** by **bbnycreamtea** ([flightsim.to](https://flightsim.to)). Thank you for the idea
and the app this version builds on. Joystick output goes through
[vJoy](https://github.com/jshafer817/vJoy).

If MouseYokeEnhanced makes your flights better, you can
[buy me a coffee](https://ko-fi.com/rangelsantosss) ☕ on Ko-fi or [sponsor it](https://github.com/sponsors/rangelsantos) on GitHub (also in
**ⓘ** → **Buy me a coffee**).

---

Enjoy the skies! ✈️
