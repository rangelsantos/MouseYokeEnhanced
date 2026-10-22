# MouseYoke — Fly with your mouse

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

The app should say **"Ready. F9 to fly."** at the top.

### Step 4 — Tell Flight Simulator about it (do this once)

1. Start Flight Simulator.
2. Go to **Options → Controls & Sensitivity**.
3. At the top, in the device list, choose **vJoy Device**.
4. Set these three:
   - **Ailerons Axis** → move your mouse **left/right** when it asks for input.
   - **Elevator Axis** → move your mouse **up/down**.
   - **Rudder Axis** → (only if you'll use keyboard rudder — see below) hold your rudder key.
5. Save. You're done setting up.

> Tip: if a control moves the wrong way, tick **Invert pitch** or **Invert roll** in MouseYoke, or use the sim's own "reverse" switch for that axis.

---

## How to fly

1. Keep MouseYoke open (on a second screen, or just behind the sim).
2. Press **F9** to start flying with the mouse. Press **F9** again to stop (handy when you need to click buttons in the cockpit).

### Your keys

- **F9** — start / stop mouse flying
- **Page Up / Page Down** — trim the nose up or down (hold)
- **Insert / Delete** — trim left or right (hold)
- **Home** — reset trim to neutral

Think of **trim** as "hold this attitude for me." Set your climb or descent, trim it in, and
the plane holds it so you're not fighting the controls.

---

## Two ways to fly — pick what feels best

On the **Flight** tab there's a checkbox called **Self-centering (spring)**.

- **Off (default):** the mouse's position on screen is the control position. Precise, but
  you have to bring the mouse back to the middle yourself.
- **On:** it feels like a real spring-loaded sidestick — nudge to turn or climb, and it
  springs back to center on its own. Most people find this **much** easier for takeoff and
  landing. Turn on **Hide cursor** too (see below) for the best experience.

Try both and keep whichever you like. You can set it differently for each aircraft (see Profiles).

---

## Making it feel just right

On the **Flight** tab you'll find some sliders. You don't need to understand them all — just
nudge and see what feels good. Rough starting points:

- **Sensitivity** — higher = more responsive, lower = gentler. Big airliners like it gentle.
- **Deadzone** — a small "dead" spot in the middle so the plane sits still when you're not moving.
- **Expo** — makes small movements gentle and big movements strong. Great for smooth landings. Try 40–50.
- **Smoothing** — smooths out shaky input.
- **Auto-center speed** — (spring mode only) how quickly the stick springs back to center.

---

## Handy extras

- **Hide cursor** — hides the mouse pointer while flying so it's not floating in your view.
  Recommended, especially in spring mode. It comes back the moment you press F9 to stop.
- **The on-screen HUD** — a little attitude circle that shows up while you're flying. Tick
  **Move HUD** to drag it anywhere you like, then untick to lock it in place. Untick **Show
  HUD** if you don't want it at all.
- **Rudder on your keyboard** — on the **Key → vJoy** tab, tick **Keyboard rudder**, pick a
  left key and a right key, and you get smooth rudder without a joystick. (Remember to bind
  Rudder Axis in the sim, Step 4 above.)
- **Extra buttons** — on the **Key → vJoy** tab you can map any key to a sim control (gear,
  flaps, brakes, autopilot…). Add a key, then bind that "button" in the sim's controls.

---

## Aircraft profiles

Fly more than one plane? The bar at the top lets you save a **profile per aircraft** — each
remembers its own feel and its own key setup.

- **New** — start a fresh profile.
- **Duplicate** — copy the current one as a starting point.
- **Rename / Delete** — tidy up your list.

Everything saves automatically. Just pick your plane's profile from the dropdown before you fly.

---

## Quick fixes

- **The plane doesn't move in the sim.** Make sure MouseYoke is open and you pressed **F9**.
  Double-check you set the axes in Step 4, and that the sim is in windowed or borderless mode.
- **My keys don't do anything.** Don't run Flight Simulator "as Administrator" — that stops
  the app from seeing your keys. Just start it normally.
- **The rudder won't set up in the sim.** The rudder only appears while it's moving, so
  **hold** your rudder key at the moment the sim asks you to move an axis. Also make sure you
  ticked **Z** in vJoy (Step 2) and turned on **Keyboard rudder** in the app.
- **The pointer got stuck invisible after a crash.** Just start MouseYoke again and press
  F9 on and off, or lock and unlock your PC (Windows key + L). Your cursor comes right back.

---

Enjoy the skies! ✈️
