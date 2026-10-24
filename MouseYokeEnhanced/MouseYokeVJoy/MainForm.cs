using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseYokeVJoy;

// The control loop, input, vJoy and profile logic. The screens are built in MainForm.Ui.cs.
public partial class MainForm : Form
{
	private struct RAWINPUTDEVICE
	{
		public ushort usUsagePage;

		public ushort usUsage;

		public uint dwFlags;

		public nint hwndTarget;
	}

	private struct RAWINPUTHEADER
	{
		public uint dwType;

		public uint dwSize;

		public nint hDevice;

		public nint wParam;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct RAWMOUSE
	{
		[FieldOffset(0)]
		public ushort usFlags;

		[FieldOffset(4)]
		public uint ulButtons;

		[FieldOffset(4)]
		public ushort usButtonFlags;

		[FieldOffset(6)]
		public ushort usButtonData;

		[FieldOffset(8)]
		public uint ulRawButtons;

		[FieldOffset(12)]
		public int lLastX;

		[FieldOffset(16)]
		public int lLastY;

		[FieldOffset(20)]
		public uint ulExtraInformation;
	}

	private struct RAWINPUT
	{
		public RAWINPUTHEADER header;

		public RAWMOUSE mouse;
	}

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const uint VJOY_ID = 1u;

	private readonly KeyboardHook hook = new KeyboardHook();

	private readonly AppSettings settings = AppSettings.Load();

	private Profile profile;

	private int vjoyButtonCount = 16;

	private Timer loop;

	private Timer saveTimer;

	private Timer settingsTimer;

	private bool active;

	private bool acquired;

	private string vjoyDetails = "";

	private double smoothX;

	private double smoothY;

	private double springX;

	private double springY;

	private double pitchTrim;

	private double rollTrim;

	// The stick before trim, as sent to vJoy. It holds during freelook, and in standby with Hold position.
	private double stickRoll;

	private double stickPitch;

	// The last direct-mode cursor position (-1..1 of the stick zone), to put the cursor back where the stick is.
	private double lastInX;

	private double lastInY;

	private bool freelook;

	// The roll input before shaping, for the dot on the Feel curve.
	private double curveInput;

	private bool trimUpHeld;

	private bool trimDnHeld;

	private bool trimLHeld;

	private bool trimRHeld;

	private int frame;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private double lastTick;

	private Rectangle flightScreen;

	private bool loading;

	private bool hudMoving;

	private bool exiting;

	private bool trayHintShown;

	private Action<int> capture;

	private OverlayForm overlay;

	private int rawDX;

	private int rawDY;

	private bool rawRegistered;

	private const int WM_INPUT = 255;

	private const uint RID_INPUT = 268435459u;

	private const uint RIM_TYPEMOUSE = 0u;

	private const uint RIDEV_INPUTSINK = 256u;

	private const ushort MOUSE_MOVE_ABSOLUTE = 1;

	private const int SM_SWAPBUTTON = 23;

	// Raw Input button-down flags and their VKs (left, right, middle, X1, X2); each button's up flag is the next bit.
	private static readonly (ushort Down, int Vk)[] MouseButtonFlags = new (ushort, int)[5]
	{
		(0x0001, 1),
		(0x0004, 2),
		(0x0010, 4),
		(0x0040, 5),
		(0x0100, 6)
	};

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(ref RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(nint lpRect);

	[DllImport("user32.dll")]
	private static extern bool GetClipCursor(out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

	public MainForm()
	{
		Text = "MouseYokeEnhanced";
		Icon = BrandIcon.Window(active: false);
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new Size(FullWidth, FullHeight);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		BackColor = Theme.Bg;
		ForeColor = Theme.Text;
		Font = Theme.Body;
		profile = ProfileManager.Bootstrap();
		saveTimer = new Timer
		{
			Interval = 300
		};
		saveTimer.Tick += (object? s, EventArgs e) =>
		{
			saveTimer.Stop();
			WriteProfile();
		};
		settingsTimer = new Timer
		{
			Interval = 300
		};
		settingsTimer.Tick += (object? s, EventArgs e) =>
		{
			settingsTimer.Stop();
			settings.Save();
		};
		loading = true;
		BuildUi();
		loading = false;
		ApplySettingsToUi();
		ApplyProfileToUi();
		AcquireVjoy();
		hook.KeyDown += OnKeyDown;
		hook.KeyUp += OnKeyUp;
		hook.Install();
		AppDomain.CurrentDomain.ProcessExit += (object? s, EventArgs e) =>
		{
			CursorHider.Restore();
			ReleaseCursor();
		};
		loop = new Timer
		{
			Interval = 10
		};
		loop.Tick += Loop_Tick;
		loop.Start();
	}

	private bool SpringMode => segMode.SelectedIndex == 1;

	private void AcquireVjoy()
	{
		if (!VJoy.vJoyEnabled())
		{
			SetStatus("vJoy driver not enabled. Install/enable vJoy, then restart.", Color.IndianRed);
			SetVjoyChip("vJoy off", ChipKind.Bad, "The vJoy driver isn't installed or is turned off.\n\nInstall vJoy (see the manual), restart the PC if it asks, then start MouseYoke again.");
			btnToggle.Enabled = false;
			return;
		}
		switch (VJoy.GetVJDStatus(1u))
		{
		case VjdStat.Free:
			acquired = VJoy.AcquireVJD(1u);
			break;
		case VjdStat.Own:
			acquired = true;
			break;
		}
		if (!acquired)
		{
			SetStatus("vJoy device 1 not free. Open 'Configure vJoy' and enable device 1.", Color.IndianRed);
			SetVjoyChip("vJoy 1 busy", ChipKind.Bad, "vJoy device 1 is missing or another app is using it.\n\nOpen Configure vJoy, make sure Device 1 is enabled, and close any other app that uses vJoy.");
			btnToggle.Enabled = false;
			return;
		}
		VJoy.ResetVJD(1u);
		int n = VJoy.GetVJDButtonNumber(1u);
		if (n > 0)
		{
			vjoyButtonCount = n;
		}
		// GetVJDAxisExist catches a device set up without an axis, which would otherwise fail silently in the sim.
		bool x = VJoy.GetVJDAxisExist(1u, VJoy.HID_USAGE_X);
		bool y = VJoy.GetVJDAxisExist(1u, VJoy.HID_USAGE_Y);
		bool z = VJoy.GetVJDAxisExist(1u, VJoy.HID_USAGE_Z);
		string axes = (x ? "X" : "") + (y ? "Y" : "") + (z ? "Z" : "");
		List<string> problems = new List<string>();
		if (!x || !y)
		{
			problems.Add("The " + ((!x && !y) ? "X and Y axes are" : ((!x) ? "X axis is" : "Y axis is")) + " off, so the mouse can't reach the sim. Open Configure vJoy and tick X and Y for Device 1.");
		}
		if (!z)
		{
			problems.Add("The Z axis is off, so keyboard rudder can't reach the sim. Open Configure vJoy and tick Z for Device 1.");
		}
		if (n < 1)
		{
			problems.Add("Device 1 has no buttons, so key → button mappings do nothing. Set Number of Buttons to 16 in Configure vJoy.");
		}
		string summary = "Driver enabled · Device 1 acquired\nAxes: X " + Mark(x) + "  Y " + Mark(y) + "  Z " + Mark(z) + "\nButtons: " + Math.Max(n, 0);
		SetVjoyChip("vJoy 1 · " + ((axes.Length > 0) ? axes : "no axes") + " · " + Math.Max(n, 0), (problems.Count == 0) ? ChipKind.Ok : ChipKind.Warn, summary + ((problems.Count > 0) ? ("\n\n" + string.Join("\n\n", problems)) : ""));
		SetStatus((profile.ToggleVk != 0) ? ("Ready. " + Profile.KeyName(profile.ToggleVk) + " to fly. Key bindings are live whenever the app runs.") : "Ready. Set a Fly / standby key on the Keys tab.", Theme.Muted);
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Button > vjoyButtonCount)
			{
				b.Button = vjoyButtonCount;
			}
		}
		RebuildBindingRows();
	}

	private static string Mark(bool ok)
	{
		return ok ? "✓" : "✗ (off)";
	}

	private void SetStatus(string text, Color color)
	{
		lblStatus.Text = text;
		lblStatus.ForeColor = color;
	}

	// Copies the UI into the profile now and writes the file a moment later, so dragging a slider
	// doesn't rewrite the JSON on every step.
	private void SaveActiveProfile()
	{
		if (!loading)
		{
			profile.RollSens = tbRollSens.Value;
			profile.PitchSens = tbPitchSens.Value;
			profile.Deadzone = tbDeadzone.Value;
			profile.Expo = tbExpo.Value;
			profile.Smoothing = tbSmooth.Value;
			profile.CenterSpeed = tbCenterSpeed.Value;
			profile.TrimRate = tbTrimRate.Value;
			profile.InvertPitch = cbInvertPitch.Checked;
			profile.InvertRoll = cbInvertRoll.Checked;
			profile.HideCursor = swHideCursor.Checked;
			profile.SelfCenter = SpringMode;
			profile.HoldPosition = swHoldPosition.Checked;
			profile.RudderEnabled = swRudder.Checked;
			profile.RudderRatePct = tbRudderRate.Value;
			saveTimer.Stop();
			saveTimer.Start();
			lblSaved.Text = "Saving…";
			lblSaved.ForeColor = Theme.Faint;
		}
	}

	private void WriteProfile()
	{
		bool ok = ProfileManager.Save(profile);
		lblSaved.Text = (ok ? "Saved" : "Not saved");
		lblSaved.ForeColor = (ok ? Theme.Faint : Theme.Bad);
		toolTip.SetToolTip(lblSaved, ok ? null : ("Couldn't write the profile to " + ProfileManager.DirPath + ". Move MouseYoke to a folder you can write to (not Program Files)."));
	}

	private void FlushSave()
	{
		if (saveTimer.Enabled)
		{
			saveTimer.Stop();
			WriteProfile();
		}
	}

	private void SaveSettingsSoon()
	{
		if (!loading)
		{
			settingsTimer.Stop();
			settingsTimer.Start();
		}
	}

	private void SwitchProfile(string name)
	{
		if (name == profile.Name)
		{
			return;
		}
		Profile next = ProfileManager.Load(name);
		if (next == null)
		{
			return;
		}
		ReleaseAllButtons();
		pitchTrim = 0.0;
		rollTrim = 0.0;
		if (!active)
		{
			ResetStick();
		}
		profile.RudderValue = 0.0;
		if (acquired)
		{
			VJoy.SetAxis(16384, 1u, 50u);
		}
		saveTimer.Stop();
		ProfileManager.Save(profile);
		profile = next;
		ProfileManager.SetActive(profile.Name);
		ClampButtonsToDevice();
		ApplyProfileToUi();
		SetStatus("Loaded profile “" + profile.Name + "”.", Color.LightGreen);
	}

	private void ClampButtonsToDevice()
	{
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Button > vjoyButtonCount)
			{
				b.Button = vjoyButtonCount;
			}
		}
	}

	// Makes the given profile current: the old one is saved first, then the UI is reloaded.
	private void ReplaceProfile(Profile p)
	{
		saveTimer.Stop();
		ProfileManager.Save(profile);
		ReleaseAllButtons();
		ProfileManager.Save(p);
		profile = p;
		ProfileManager.SetActive(p.Name);
		ClampButtonsToDevice();
		ApplyProfileToUi();
	}

	private void NewProfile()
	{
		string name = Prompt("New profile", "Aircraft / profile name:", "New Aircraft");
		if (!string.IsNullOrWhiteSpace(name))
		{
			ReplaceProfile(new Profile
			{
				Name = UniqueName(ProfileManager.Sanitize(name))
			});
		}
	}

	private void DuplicateProfile()
	{
		string name = Prompt("Duplicate profile", "Name for the copy:", profile.Name + " copy");
		if (!string.IsNullOrWhiteSpace(name))
		{
			SaveActiveProfile();
			ReplaceProfile(ProfileManager.Clone(profile, UniqueName(ProfileManager.Sanitize(name))));
		}
	}

	private void RenameProfile()
	{
		string name = Prompt("Rename profile", "New name:", profile.Name);
		if (string.IsNullOrWhiteSpace(name))
		{
			return;
		}
		name = ProfileManager.Sanitize(name);
		if (name == profile.Name)
		{
			return;
		}
		// A change of case only ("cessna" to "Cessna") keeps the same file, so it isn't a clash.
		if (!string.Equals(name, profile.Name, StringComparison.OrdinalIgnoreCase))
		{
			name = UniqueName(name);
		}
		string old = profile.Name;
		saveTimer.Stop();
		profile.Name = name;
		if (!ProfileManager.Rename(profile, old))
		{
			profile.Name = old;
			MessageBox.Show("Couldn't rename the profile file.", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		ProfileManager.SetActive(name);
		RefreshProfileUi();
	}

	private void DeleteProfile()
	{
		if (ProfileManager.List().Count <= 1)
		{
			MessageBox.Show("You need at least one profile.", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (MessageBox.Show("Delete profile “" + profile.Name + "”?", "MouseYokeEnhanced", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
		{
			saveTimer.Stop();
			ReleaseAllButtons();
			ProfileManager.Delete(profile.Name);
			List<string> remaining = ProfileManager.List();
			profile = ProfileManager.Load(remaining[0]) ?? new Profile
			{
				Name = remaining[0]
			};
			ProfileManager.SetActive(profile.Name);
			ClampButtonsToDevice();
			ApplyProfileToUi();
		}
	}

	private void ImportProfile()
	{
		using OpenFileDialog dlg = new OpenFileDialog
		{
			Title = "Import profile",
			Filter = "MouseYoke profile (*.json)|*.json|All files (*.*)|*.*"
		};
		if (dlg.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		Profile p = ProfileManager.Import(dlg.FileName);
		if (p == null)
		{
			MessageBox.Show("That file isn't a MouseYoke profile, or it can't be read.", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		p.Buttons ??= new List<ButtonBinding>();
		p.Name = UniqueName(ProfileManager.Sanitize(Path.GetFileNameWithoutExtension(dlg.FileName)));
		ReplaceProfile(p);
		SetStatus("Imported profile “" + p.Name + "”.", Color.LightGreen);
	}

	private void ExportProfile()
	{
		using SaveFileDialog dlg = new SaveFileDialog
		{
			Title = "Export profile",
			Filter = "MouseYoke profile (*.json)|*.json",
			FileName = profile.Name + ".json"
		};
		if (dlg.ShowDialog(this) == DialogResult.OK)
		{
			FlushSave();
			if (ProfileManager.Export(profile, dlg.FileName))
			{
				SetStatus("Exported “" + profile.Name + "”.", Color.LightGreen);
			}
			else
			{
				MessageBox.Show("Couldn't write " + dlg.FileName + ".", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}

	private void OpenProfilesFolder()
	{
		try
		{
			ProfileManager.EnsureDir();
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ProfileManager.DirPath + "\"")
			{
				UseShellExecute = true
			});
		}
		catch
		{
			SetStatus("Couldn't open " + ProfileManager.DirPath, Color.IndianRed);
		}
	}

	private static string UniqueName(string name)
	{
		if (!ProfileManager.Exists(name))
		{
			return name;
		}
		for (int i = 2; i < 100; i++)
		{
			string candidate = $"{name} {i}";
			if (!ProfileManager.Exists(candidate))
			{
				return candidate;
			}
		}
		return name + " " + DateTime.Now.Ticks;
	}

	private void ReleaseAllButtons()
	{
		if (!acquired)
		{
			return;
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			VJoy.SetBtn(Value: false, 1u, (byte)b.Button);
			b.Pressed = false;
		}
	}

	private void OnKeyDown(int vk)
	{
		if (capture != null)
		{
			Action<int> cb = capture;
			capture = null;
			BeginInvoke(() =>
			{
				cb(vk);
			});
			return;
		}
		if (vk == profile.ToggleVk)
		{
			ToggleActive();
			return;
		}
		if (vk == profile.FreelookVk)
		{
			SetFreelook(on: true);
			return;
		}
		if (vk == profile.CenterVk)
		{
			CenterStick();
			HudToast("CENTER");
			return;
		}
		if (vk == profile.TrimUpVk)
		{
			trimUpHeld = true;
			return;
		}
		if (vk == profile.TrimDownVk)
		{
			trimDnHeld = true;
			return;
		}
		if (vk == profile.TrimLeftVk)
		{
			trimLHeld = true;
			return;
		}
		if (vk == profile.TrimRightVk)
		{
			trimRHeld = true;
			return;
		}
		if (vk == profile.TrimResetVk)
		{
			pitchTrim = 0.0;
			rollTrim = 0.0;
			HudToast("TRIM RESET");
			return;
		}
		if (profile.RudderEnabled)
		{
			if (vk == profile.RudderLeftVk)
			{
				profile.RudderLeftHeld = true;
			}
			if (vk == profile.RudderRightVk)
			{
				profile.RudderRightHeld = true;
			}
		}
		if (!acquired)
		{
			return;
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk)
			{
				b.Pressed = true;
				VJoy.SetBtn(Value: true, 1u, (byte)b.Button);
			}
		}
	}

	private void OnKeyUp(int vk)
	{
		if (vk == profile.FreelookVk)
		{
			SetFreelook(on: false);
		}
		if (vk == profile.TrimUpVk)
		{
			trimUpHeld = false;
		}
		if (vk == profile.TrimDownVk)
		{
			trimDnHeld = false;
		}
		if (vk == profile.TrimLeftVk)
		{
			trimLHeld = false;
		}
		if (vk == profile.TrimRightVk)
		{
			trimRHeld = false;
		}
		if (vk == profile.RudderLeftVk)
		{
			profile.RudderLeftHeld = false;
		}
		if (vk == profile.RudderRightVk)
		{
			profile.RudderRightHeld = false;
		}
		if (!acquired)
		{
			return;
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk)
			{
				b.Pressed = false;
				VJoy.SetBtn(Value: false, 1u, (byte)b.Button);
			}
		}
	}

	private void ToggleActive()
	{
		if (!acquired)
		{
			return;
		}
		active = !active;
		if (active)
		{
			flightScreen = Screen.PrimaryScreen.Bounds;
			if (!swHoldPosition.Checked)
			{
				ResetStick();
			}
			rawDX = (rawDY = 0);
			if (!freelook)
			{
				ConfineCursor();
				PlaceCursor();
			}
			if (swHideCursor.Checked)
			{
				CursorHider.Hide();
			}
			SetStatus("Flying. Mouse = stick. Trim holds attitude." + ((profile.ToggleVk != 0) ? (" " + Profile.KeyName(profile.ToggleVk) + " to release.") : ""), Color.LightGreen);
		}
		else
		{
			CursorHider.Restore();
			ReleaseCursor();
			SetStatus(swHoldPosition.Checked ? "Standby. Stick and trim hold; mouse free for the cockpit." : "Standby. Trim holds; mouse free for the cockpit.", Theme.Muted);
		}
		btnToggle.Flying = active;
		Icon = BrandIcon.Window(active);
		tray.Icon = BrandIcon.Tray(active);
		RefreshCompact();
		HudToast(active ? "ACTIVE" : "STANDBY");
		UpdateOverlayVisibility();
	}

	// Keeps the cursor on the flight screen while active, so crossing onto another monitor
	// can't flip the control from one extreme to the other. Windows or other apps may drop
	// the clip (focus changes, Ctrl+Alt+Del...), so the loop re-asserts it every tick.
	private void ConfineCursor()
	{
		GetClipCursor(out var cur);
		if (cur.Left != flightScreen.Left || cur.Top != flightScreen.Top || cur.Right != flightScreen.Right || cur.Bottom != flightScreen.Bottom)
		{
			RECT r = new RECT
			{
				Left = flightScreen.Left,
				Top = flightScreen.Top,
				Right = flightScreen.Right,
				Bottom = flightScreen.Bottom
			};
			ClipCursor(ref r);
		}
	}

	private static void ReleaseCursor()
	{
		ClipCursor(IntPtr.Zero);
	}

	// The share of the half-screen that gives full deflection in direct mode: more sensitivity, smaller zone.
	private static double Zone(int sens)
	{
		return 1.0 - (double)(sens - 1) / 99.0 * 0.8;
	}

	// Puts the cursor where the stick is, so flying picks up without a jump: on the held position in
	// direct mode, at the center in spring mode (which reads only movement).
	private void PlaceCursor()
	{
		Rectangle b = flightScreen;
		double x = (double)b.Left + (double)b.Width / 2.0;
		double y = (double)b.Top + (double)b.Height / 2.0;
		if (!SpringMode)
		{
			x += lastInX * ((double)b.Width / 2.0 * Zone(tbRollSens.Value));
			y += lastInY * ((double)b.Height / 2.0 * Zone(tbPitchSens.Value));
		}
		SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
	}

	private void ResetStick()
	{
		smoothX = (smoothY = 0.0);
		springX = (springY = 0.0);
		lastInX = (lastInY = 0.0);
		stickRoll = (stickPitch = 0.0);
		curveInput = 0.0;
	}

	// Stick and rudder back to center; the trims stay. Flying in direct mode the cursor goes to the
	// center and smoothing carries the stick there, as if the mouse had moved; otherwise it centers at once.
	private void CenterStick()
	{
		bool flying = active && !freelook;
		double sx = smoothX;
		double sy = smoothY;
		ResetStick();
		profile.RudderValue = 0.0;
		if (flying && !SpringMode)
		{
			smoothX = sx;
			smoothY = sy;
		}
		if (flying)
		{
			rawDX = (rawDY = 0);
			PlaceCursor();
		}
	}

	// While the freelook key is held the mouse is free and the stick holds where it was. On release
	// the cursor goes back to the stick's position, so flying picks up exactly from there.
	private void SetFreelook(bool on)
	{
		if (freelook == on)
		{
			return;
		}
		freelook = on;
		if (!active)
		{
			return;
		}
		if (on)
		{
			ReleaseCursor();
			return;
		}
		rawDX = (rawDY = 0);
		ConfineCursor();
		PlaceCursor();
	}

	private void EnsureOverlay()
	{
		if (overlay == null || overlay.IsDisposed)
		{
			overlay = new OverlayForm(settings.HudStyle, settings.HudNumbers, CurrentHudState());
			overlay.SetOpacityPercent(settings.HudOpacity);
			overlay.SetFade(settings.HudFade);
			overlay.DoneClicked += EndHudPosition;
			if (hudMoving)
			{
				overlay.SetMovable(on: true);
			}
		}
	}

	// The HUD shows while flying, while being positioned, in standby if asked, and briefly for a notice.
	private void UpdateOverlayVisibility()
	{
		bool want = hudMoving || (settings.ShowHud && (active || settings.HudInStandby || (overlay != null && !overlay.IsDisposed && overlay.ToastActive)));
		if (want)
		{
			EnsureOverlay();
			if (!overlay.Visible)
			{
				overlay.UpdateValues(CurrentHudState());
				overlay.Show();
			}
		}
		else if (overlay != null && !overlay.IsDisposed && overlay.Visible)
		{
			overlay.Hide();
		}
	}

	private void HudToast(string text)
	{
		if (settings.ShowHud)
		{
			EnsureOverlay();
			overlay.Toast(text);
			UpdateOverlayVisibility();
		}
	}

	private void BeginHudPosition()
	{
		hudMoving = true;
		EnsureOverlay();
		overlay.SetMovable(on: true);
		UpdateOverlayVisibility();
		btnPositionHud.Text = "Done";
		SetStatus("Drag the HUD where you want it, then click Done.", Color.DeepSkyBlue);
	}

	private void EndHudPosition()
	{
		if (hudMoving)
		{
			hudMoving = false;
			overlay?.SetMovable(on: false);
			UpdateOverlayVisibility();
			btnPositionHud.Text = "Position HUD…";
			SetStatus("HUD position saved.", Theme.Muted);
		}
	}

	private double lastOutRoll;

	private double lastOutPitch;

	private HudState CurrentHudState()
	{
		return new HudState
		{
			Roll = lastOutRoll,
			Pitch = lastOutPitch,
			Rudder = (profile.RudderEnabled ? profile.RudderValue : 0.0),
			PitchTrim = pitchTrim,
			RollTrim = rollTrim,
			Deadzone = (double)tbDeadzone.Value / 100.0,
			Active = active && !freelook,
			Freelook = active && freelook,
			Spring = SpringMode,
			RudderOn = profile.RudderEnabled,
			ToggleKey = ((profile.ToggleVk != 0) ? Profile.KeyName(profile.ToggleVk) : ""),
			ShowRudder = HudShows(settings.HudRudder, RudderInUse),
			ShowPitchTrim = HudShows(settings.HudPitchTrim, PitchTrimKeysSet || pitchTrim != 0.0),
			ShowRollTrim = HudShows(settings.HudRollTrim, RollTrimKeysSet || rollTrim != 0.0)
		};
	}

	// What Auto looks at. A trim also counts while it is still applied, even with its keys unbound,
	// so the HUD never hides an offset that is pulling the aircraft.
	private bool RudderInUse => profile.RudderEnabled && (profile.RudderLeftVk != 0 || profile.RudderRightVk != 0);

	private bool PitchTrimKeysSet => profile.TrimUpVk != 0 || profile.TrimDownVk != 0;

	private bool RollTrimKeysSet => profile.TrimLeftVk != 0 || profile.TrimRightVk != 0;

	private static bool HudShows(HudElementMode mode, bool inUse)
	{
		return mode == HudElementMode.Show || (mode == HudElementMode.Auto && inUse);
	}

	private void Loop_Tick(object sender, EventArgs e)
	{
		// WM_TIMER is rounded to the ~15.6 ms system tick (~64 Hz, not 100 Hz), so every rate is
		// integrated over the real elapsed time. Capped so a UI stall can't cause a big jump.
		double now = clock.Elapsed.TotalSeconds;
		double dt = Math.Min(now - lastTick, 0.1);
		lastTick = now;
		hook.ReleaseStaleKeys();
		if (!acquired)
		{
			return;
		}
		bool flying = active && !freelook;
		if (flying)
		{
			ConfineCursor();
		}
		double trimStep = (double)tbTrimRate.Value / 100.0 * dt;
		if (trimUpHeld)
		{
			pitchTrim = Clamp(pitchTrim - trimStep, -1.0, 1.0);
		}
		if (trimDnHeld)
		{
			pitchTrim = Clamp(pitchTrim + trimStep, -1.0, 1.0);
		}
		if (trimLHeld)
		{
			rollTrim = Clamp(rollTrim - trimStep, -1.0, 1.0);
		}
		if (trimRHeld)
		{
			rollTrim = Clamp(rollTrim + trimStep, -1.0, 1.0);
		}
		if (flying && SpringMode)
		{
			Rectangle ps = flightScreen;
			double dx;
			double dy;
			if (rawRegistered)
			{
				dx = rawDX;
				dy = rawDY;
				rawDX = 0;
				rawDY = 0;
				if (swHideCursor.Checked)
				{
					SetCursorPos(ps.Right - 1, ps.Bottom - 1);
				}
				else
				{
					SetCursorPos(ps.Left + ps.Width / 2, ps.Top + ps.Height / 2);
				}
			}
			else
			{
				Rectangle scr = flightScreen;
				int cxp = scr.Left + scr.Width / 2;
				int cyp = scr.Top + scr.Height / 2;
				Point cur = System.Windows.Forms.Cursor.Position;
				dx = cur.X - cxp;
				dy = cur.Y - cyp;
				SetCursorPos(cxp, cyp);
			}
			double baseGain = (rawRegistered ? 0.03 : 0.015);
			springX += dx * ((double)tbRollSens.Value / 100.0 * baseGain);
			springY += dy * ((double)tbPitchSens.Value / 100.0 * baseGain);
			// Spring and smoothing were tuned as per-10 ms-tick factors; compounding them over
			// dt keeps the same feel at whatever rate the timer actually runs.
			double decay = 1.0 - Math.Pow(1.0 - Math.Min((double)tbCenterSpeed.Value / 100.0 * 0.01, 1.0), dt * 100.0);
			springX -= springX * decay;
			springY -= springY * decay;
			springX = Clamp(springX, -1.0, 1.0);
			springY = Clamp(springY, -1.0, 1.0);
			curveInput = springX;
			double expo = (double)tbExpo.Value / 100.0;
			double ox = Shape(springX, 0.0, expo);
			double oy = Shape(springY, 0.0, expo);
			if (cbInvertRoll.Checked)
			{
				ox = 0.0 - ox;
			}
			if (cbInvertPitch.Checked)
			{
				oy = 0.0 - oy;
			}
			stickRoll = ox;
			stickPitch = oy;
		}
		else if (flying)
		{
			rawDX = 0;
			rawDY = 0;
			Rectangle scr2 = flightScreen;
			double cx = (double)scr2.Left + (double)scr2.Width / 2.0;
			double cy = (double)scr2.Top + (double)scr2.Height / 2.0;
			double nx = Clamp(((double)System.Windows.Forms.Cursor.Position.X - cx) / ((double)scr2.Width / 2.0 * Zone(tbRollSens.Value)), -1.0, 1.0);
			double ny = Clamp(((double)System.Windows.Forms.Cursor.Position.Y - cy) / ((double)scr2.Height / 2.0 * Zone(tbPitchSens.Value)), -1.0, 1.0);
			lastInX = nx;
			lastInY = ny;
			curveInput = nx;
			double dz = (double)tbDeadzone.Value / 100.0;
			double expo2 = (double)tbExpo.Value / 100.0;
			nx = Shape(nx, dz, expo2);
			ny = Shape(ny, dz, expo2);
			if (cbInvertRoll.Checked)
			{
				nx = 0.0 - nx;
			}
			if (cbInvertPitch.Checked)
			{
				ny = 0.0 - ny;
			}
			double a = 1.0 - Math.Pow((double)tbSmooth.Value / 100.0, dt * 100.0);
			smoothX += (nx - smoothX) * a;
			smoothY += (ny - smoothY) * a;
			stickRoll = smoothX;
			stickPitch = smoothY;
		}
		else
		{
			// The mouse is free: the stick holds during freelook, and in standby only with Hold position.
			rawDX = 0;
			rawDY = 0;
			if (!active && !swHoldPosition.Checked)
			{
				ResetStick();
			}
		}
		double outRoll = Clamp(stickRoll + rollTrim, -1.0, 1.0);
		double outPitch = Clamp(stickPitch + pitchTrim, -1.0, 1.0);
		lastOutRoll = outRoll;
		lastOutPitch = outPitch;
		VJoy.SetAxis(ToAxis(outRoll), 1u, 48u);
		VJoy.SetAxis(ToAxis(outPitch), 1u, 49u);
		if (profile.RudderEnabled)
		{
			double rstep = (double)tbRudderRate.Value / 100.0 * dt;
			if (profile.RudderLeftHeld && !profile.RudderRightHeld)
			{
				profile.RudderValue = Clamp(profile.RudderValue - rstep, -1.0, 1.0);
			}
			else if (profile.RudderRightHeld && !profile.RudderLeftHeld)
			{
				profile.RudderValue = Clamp(profile.RudderValue + rstep, -1.0, 1.0);
			}
			else if (profile.RudderValue > 0.0)
			{
				profile.RudderValue = Math.Max(0.0, profile.RudderValue - rstep);
			}
			else if (profile.RudderValue < 0.0)
			{
				profile.RudderValue = Math.Min(0.0, profile.RudderValue + rstep);
			}
			VJoy.SetAxis(ToAxis(profile.RudderValue), 1u, 50u);
		}
		if (++frame % 3 != 0)
		{
			return;
		}
		UpdateOverlayVisibility();
		HudState st = CurrentHudState();
		if (overlay != null && !overlay.IsDisposed && overlay.Visible)
		{
			overlay.UpdateValues(st);
		}
		RefreshLiveUi(st);
	}

	internal static double Shape(double x, double dz, double expo)
	{
		double s = Math.Sign(x);
		x = Math.Abs(x);
		if (x < dz)
		{
			return 0.0;
		}
		x = (x - dz) / (1.0 - dz);
		x = (1.0 - expo) * x + expo * x * x * x;
		return s * x;
	}

	private static int ToAxis(double n)
	{
		double v = (n + 1.0) / 2.0;
		int axis = (int)Math.Round(1.0 + v * 32767.0);
		return Math.Max(1, Math.Min(32768, axis));
	}

	private static int Clamp(int v, int lo, int hi)
	{
		if (v >= lo)
		{
			if (v <= hi)
			{
				return v;
			}
			return hi;
		}
		return lo;
	}

	private static double Clamp(double v, double lo, double hi)
	{
		if (!(v < lo))
		{
			if (!(v > hi))
			{
				return v;
			}
			return hi;
		}
		return lo;
	}

	private void ShowFromTray()
	{
		Show();
		if (WindowState == FormWindowState.Minimized)
		{
			WindowState = FormWindowState.Normal;
		}
		Activate();
	}

	private void ExitApp()
	{
		exiting = true;
		Close();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (settings.CloseToTray && !exiting && e.CloseReason == CloseReason.UserClosing)
		{
			e.Cancel = true;
			Hide();
			if (!trayHintShown)
			{
				trayHintShown = true;
				tray.ShowBalloonTip(3000, "MouseYoke is still running", "Your keys keep working. Right-click this icon to exit.", ToolTipIcon.Info);
			}
			return;
		}
		CursorHider.Restore();
		ReleaseCursor();
		SaveActiveProfile();
		FlushSave();
		settingsTimer.Stop();
		settings.Save();
		hook.Dispose();
		if (acquired)
		{
			VJoy.ResetVJD(1u);
			VJoy.RelinquishVJD(1u);
		}
		overlay?.Close();
		tray.Visible = false;
		tray.Dispose();
		base.OnFormClosing(e);
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

	[DllImport("user32.dll")]
	private static extern uint GetRawInputData(nint hRawInput, uint uiCommand, nint pData, ref uint pcbSize, uint cbSizeHeader);

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int dark = 1;
		DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
		if (!rawRegistered)
		{
			RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
			rid[0].usUsagePage = 1;
			rid[0].usUsage = 2;
			rid[0].dwFlags = 256u;
			rid[0].hwndTarget = Handle;
			rawRegistered = RegisterRawInputDevices(rid, 1u, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
		}
	}

	// Raw Input sees the buttons even while the sim has focus, but it can't swallow the click.
	// The primary button (left, or right when swapped) stays out: it is the one that clicks the
	// UI and the cockpit. Raw Input and GetAsyncKeyState both report physical buttons.
	private void OnMouseButtons(ushort flags)
	{
		int primary = ((GetSystemMetrics(SM_SWAPBUTTON) != 0) ? 2 : 1);
		foreach ((ushort down, int vk) in MouseButtonFlags)
		{
			if (vk == primary)
			{
				continue;
			}
			if ((flags & down) != 0)
			{
				hook.MouseButton(vk, down: true);
			}
			if ((flags & (down << 1)) != 0)
			{
				hook.MouseButton(vk, down: false);
			}
		}
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 255)
		{
			uint size = 0u;
			uint hdr = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
			GetRawInputData(m.LParam, 268435459u, IntPtr.Zero, ref size, hdr);
			if (size != 0)
			{
				nint buf = Marshal.AllocHGlobal((int)size);
				try
				{
					if (GetRawInputData(m.LParam, 268435459u, buf, ref size, hdr) == size)
					{
						RAWINPUT ri = Marshal.PtrToStructure<RAWINPUT>(buf);
						if (ri.header.dwType == 0 && (ri.mouse.usFlags & 1) == 0)
						{
							rawDX += ri.mouse.lLastX;
							rawDY += ri.mouse.lLastY;
						}
						if (ri.header.dwType == 0 && ri.mouse.usButtonFlags != 0)
						{
							OnMouseButtons(ri.mouse.usButtonFlags);
						}
					}
				}
				finally
				{
					Marshal.FreeHGlobal(buf);
				}
			}
		}
		base.WndProc(ref m);
	}
}
