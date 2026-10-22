using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseYokeVJoy;

public class MainForm : Form
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

	private sealed class HotkeyRow
	{
		public Hotkey Hotkey;

		public Button Key;

		public Button Clear;

		public Control ConflictBar;

		public Label ConflictText;
	}

	private const uint VJOY_ID = 1u;

	private readonly KeyboardHook hook = new KeyboardHook();

	private Profile profile;

	private int vjoyButtonCount = 16;

	private Timer loop;

	private bool active;

	private bool acquired;

	private double smoothX;

	private double smoothY;

	private double springX;

	private double springY;

	private double pitchTrim;

	private double rollTrim;

	private bool trimUpHeld;

	private bool trimDnHeld;

	private bool trimLHeld;

	private bool trimRHeld;

	private int frame;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private double lastTick;

	private Rectangle flightScreen;

	private bool loading;

	private bool switching;

	private Action<int> capture;

	private readonly List<HotkeyRow> hotkeyRows = new List<HotkeyRow>();

	private HotkeyRow capturingRow;

	private HotkeyRow conflictRow;

	private int conflictVk;

	private Hotkey conflictWith;

	private string resetHint = "";

	private OverlayForm overlay;

	private ComboBox cbProfiles;

	private Button btnToggle;

	private Label lblStatus;

	private Label lblAxis;

	private Label lblTrim;

	private Label lblHotkeyHelp;

	private TrackBar tbRollSens;

	private TrackBar tbPitchSens;

	private TrackBar tbDeadzone;

	private TrackBar tbExpo;

	private TrackBar tbSmooth;

	private TrackBar tbTrimRate;

	private TrackBar tbCenterSpeed;

	private TrackBar tbHudSize;

	private CheckBox cbInvertPitch;

	private CheckBox cbInvertRoll;

	private CheckBox cbTopMost;

	private CheckBox cbOverlay;

	private CheckBox cbMoveHud;

	private CheckBox cbHideCursor;

	private CheckBox cbSelfCenter;

	private FlowLayoutPanel bindPanel;

	private Label lblBindStatus;

	private CheckBox cbRudder;

	private TrackBar tbRudderRate;

	private readonly List<(ButtonBinding b, Panel lamp)> bindRows = new List<(ButtonBinding, Panel)>();

	private static readonly Color Bg = Color.FromArgb(18, 18, 22);

	private int rawDX;

	private int rawDY;

	private bool rawRegistered;

	private const int WM_INPUT = 255;

	private const uint RID_INPUT = 268435459u;

	private const uint RIM_TYPEMOUSE = 0u;

	private const uint RIDEV_INPUTSINK = 256u;

	private const ushort MOUSE_MOVE_ABSOLUTE = 1;

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(ref RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(nint lpRect);

	[DllImport("user32.dll")]
	private static extern bool GetClipCursor(out RECT lpRect);

	public MainForm()
	{
		Text = "MouseYokeEnhanced — vJoy";
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new Size(470, 720);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		BackColor = Bg;
		ForeColor = Color.Gainsboro;
		Font = new Font("Segoe UI", 9f);
		profile = ProfileManager.Bootstrap();
		BuildUi();
		RefreshProfileCombo();
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

	private void AcquireVjoy()
	{
		if (!VJoy.vJoyEnabled())
		{
			SetStatus("vJoy driver not enabled. Install/enable vJoy, then restart.", Color.IndianRed);
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
			btnToggle.Enabled = false;
			return;
		}
		VJoy.ResetVJD(1u);
		int n = VJoy.GetVJDButtonNumber(1u);
		if (n > 0)
		{
			vjoyButtonCount = n;
		}
		SetStatus((profile.ToggleVk != 0) ? ("Ready. " + Profile.KeyName(profile.ToggleVk) + " to fly. Key bindings are live whenever the app runs.") : "Ready. Set a Fly / standby key on the Hotkeys tab.", Color.DarkGray);
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Button > vjoyButtonCount)
			{
				b.Button = vjoyButtonCount;
			}
		}
		RebuildBindingRows();
	}

	private void SetStatus(string text, Color color)
	{
		lblStatus.Text = text;
		lblStatus.ForeColor = color;
	}

	private TableLayoutPanel NewColumn()
	{
		return new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			AutoScroll = true,
			Padding = new Padding(12, 10, 12, 10),
			BackColor = Bg,
			GrowStyle = TableLayoutPanelGrowStyle.AddRows,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
	}

	private void AddRow(TableLayoutPanel t, ref int row, Control c, SizeType type, int size, bool fill = true)
	{
		t.RowStyles.Add(new RowStyle(type, size));
		if (fill)
		{
			c.Dock = DockStyle.Fill;
		}
		else
		{
			c.Anchor = AnchorStyles.Left;
		}
		if (c.Margin == new Padding(3))
		{
			c.Margin = new Padding(2, 3, 2, 3);
		}
		t.Controls.Add(c, 0, row);
		row++;
	}

	private TrackBar AddSliderRow(TableLayoutPanel t, ref int row, string label, int min, int max, int val, bool autosave = true)
	{
		AddRow(t, ref row, new Label
		{
			Text = label,
			ForeColor = Color.Silver,
			AutoSize = true
		}, SizeType.AutoSize, 0, fill: false);
		TrackBar tb = new TrackBar
		{
			Minimum = min,
			Maximum = max,
			Value = Math.Min(Math.Max(val, min), max),
			TickStyle = TickStyle.None,
			BackColor = Bg
		};
		if (autosave)
		{
			tb.ValueChanged += (object? s, EventArgs e) =>
			{
				SaveActiveProfile();
			};
		}
		AddRow(t, ref row, tb, SizeType.Absolute, 40);
		return tb;
	}

	private Button Styled(string text, Color back)
	{
		Button button = new Button();
		button.Text = text;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = back;
		button.ForeColor = Color.White;
		button.AutoSize = false;
		button.Font = new Font("Segoe UI", 9f);
		button.FlatAppearance.BorderSize = 0;
		return button;
	}

	private void BuildUi()
	{
		TableLayoutPanel root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Bg
		};
		root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Controls.Add(root);
		FlowLayoutPanel bar = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			WrapContents = true,
			BackColor = Color.FromArgb(28, 28, 36),
			Padding = new Padding(8, 6, 8, 6)
		};
		bar.Controls.Add(new Label
		{
			Text = "Aircraft profile",
			ForeColor = Color.Silver,
			AutoSize = true,
			Margin = new Padding(3, 9, 6, 0)
		});
		cbProfiles = new ComboBox
		{
			Width = 150,
			DropDownStyle = ComboBoxStyle.DropDownList,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(45, 45, 55),
			ForeColor = Color.White,
			Margin = new Padding(0, 5, 8, 0)
		};
		cbProfiles.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			if (!switching && cbProfiles.SelectedItem != null)
			{
				SwitchProfile((string)cbProfiles.SelectedItem);
			}
		};
		bar.Controls.Add(cbProfiles);
		bar.Controls.Add(TopBtn("New", (object? s, EventArgs e) =>
		{
			NewProfile();
		}));
		bar.Controls.Add(TopBtn("Duplicate", (object? s, EventArgs e) =>
		{
			DuplicateProfile();
		}));
		bar.Controls.Add(TopBtn("Rename", (object? s, EventArgs e) =>
		{
			RenameProfile();
		}));
		bar.Controls.Add(TopBtn("Delete", (object? s, EventArgs e) =>
		{
			DeleteProfile();
		}, Color.FromArgb(90, 40, 40)));
		root.Controls.Add(bar, 0, 0);
		TabControl tabs = new TabControl
		{
			Dock = DockStyle.Fill
		};
		TabPage tabFlight = new TabPage("Flight")
		{
			BackColor = Bg
		};
		TabPage tabKeys = new TabPage("Key → vJoy")
		{
			BackColor = Bg
		};
		TabPage tabHotkeys = new TabPage("Hotkeys")
		{
			BackColor = Bg
		};
		tabs.TabPages.Add(tabFlight);
		tabs.TabPages.Add(tabKeys);
		tabs.TabPages.Add(tabHotkeys);
		root.Controls.Add(tabs, 0, 1);
		BuildFlightTab(tabFlight);
		BuildKeysTab(tabKeys);
		BuildHotkeysTab(tabHotkeys);
	}

	private Button TopBtn(string text, EventHandler onClick, Color? color = null)
	{
		Button button = Styled(text, color ?? Color.FromArgb(45, 55, 75));
		button.AutoSize = true;
		button.Margin = new Padding(3, 4, 0, 2);
		button.Padding = new Padding(6, 2, 6, 2);
		button.Font = new Font("Segoe UI", 8.5f);
		button.Click += onClick;
		return button;
	}

	private void BuildFlightTab(Control host)
	{
		TableLayoutPanel t = NewColumn();
		host.Controls.Add(t);
		int row = 0;
		btnToggle = Styled("Activate", Color.FromArgb(40, 120, 90));
		btnToggle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
		btnToggle.Margin = new Padding(2, 3, 2, 6);
		btnToggle.Click += (object? s, EventArgs e) =>
		{
			ToggleActive();
		};
		AddRow(t, ref row, btnToggle, SizeType.Absolute, 46);
		lblStatus = new Label
		{
			ForeColor = Color.DarkGray,
			AutoSize = true
		};
		AddRow(t, ref row, lblStatus, SizeType.AutoSize, 0, fill: false);
		lblAxis = new Label
		{
			ForeColor = Color.SteelBlue,
			AutoSize = true
		};
		AddRow(t, ref row, lblAxis, SizeType.AutoSize, 0, fill: false);
		lblTrim = new Label
		{
			ForeColor = Color.Goldenrod,
			AutoSize = true,
			Margin = new Padding(2, 3, 2, 8)
		};
		AddRow(t, ref row, lblTrim, SizeType.AutoSize, 0, fill: false);
		tbRollSens = AddSliderRow(t, ref row, "Roll sensitivity", 1, 100, 55);
		tbPitchSens = AddSliderRow(t, ref row, "Pitch sensitivity", 1, 100, 45);
		tbDeadzone = AddSliderRow(t, ref row, "Deadzone %", 0, 30, 4);
		tbExpo = AddSliderRow(t, ref row, "Expo %", 0, 90, 40);
		tbSmooth = AddSliderRow(t, ref row, "Smoothing %", 0, 80, 15);
		tbCenterSpeed = AddSliderRow(t, ref row, "Auto-center speed (%/sec, spring mode)", 10, 300, 120);
		tbTrimRate = AddSliderRow(t, ref row, "Trim rate (%/sec, hold a trim key)", 1, 100, 25);
		FlowLayoutPanel checks = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = true,
			Margin = new Padding(0, 6, 0, 0)
		};
		cbInvertPitch = new CheckBox
		{
			Text = "Invert pitch",
			ForeColor = ForeColor,
			AutoSize = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbInvertRoll = new CheckBox
		{
			Text = "Invert roll",
			ForeColor = ForeColor,
			AutoSize = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbSelfCenter = new CheckBox
		{
			Text = "Self-centering (spring)",
			ForeColor = ForeColor,
			AutoSize = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbOverlay = new CheckBox
		{
			Text = "Show HUD",
			ForeColor = ForeColor,
			AutoSize = true,
			Checked = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbMoveHud = new CheckBox
		{
			Text = "Move HUD",
			ForeColor = ForeColor,
			AutoSize = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbHideCursor = new CheckBox
		{
			Text = "Hide cursor",
			ForeColor = ForeColor,
			AutoSize = true,
			Checked = true,
			Margin = new Padding(2, 4, 12, 4)
		};
		cbTopMost = new CheckBox
		{
			Text = "On top",
			ForeColor = ForeColor,
			AutoSize = true,
			Checked = true,
			Margin = new Padding(2, 4, 2, 4)
		};
		cbInvertPitch.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
		};
		cbInvertRoll.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
		};
		cbSelfCenter.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			springX = (springY = 0.0);
			if (active)
			{
				Rectangle bounds = Screen.PrimaryScreen.Bounds;
				SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
			}
		};
		cbOverlay.CheckedChanged += (object? s, EventArgs e) =>
		{
			if (overlay != null)
			{
				overlay.Visible = cbOverlay.Checked && (active || cbMoveHud.Checked);
			}
			if (!cbOverlay.Checked && cbMoveHud.Checked)
			{
				cbMoveHud.Checked = false;
			}
		};
		cbMoveHud.CheckedChanged += (object? s, EventArgs e) =>
		{
			if (cbMoveHud.Checked)
			{
				if (!cbOverlay.Checked)
				{
					cbOverlay.Checked = true;
				}
				EnsureOverlay();
				overlay.Visible = true;
				overlay.SetMovable(on: true);
				SetStatus("Drag the HUD where you want it, then untick Move HUD to lock it.", Color.DeepSkyBlue);
			}
			else
			{
				overlay?.SetMovable(on: false);
				if (overlay != null && !active)
				{
					overlay.Visible = false;
				}
			}
		};
		cbTopMost.CheckedChanged += (object? s, EventArgs e) =>
		{
			TopMost = cbTopMost.Checked;
		};
		cbHideCursor.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			if (active)
			{
				if (cbHideCursor.Checked)
				{
					CursorHider.Hide();
				}
				else
				{
					CursorHider.Restore();
				}
			}
		};
		TopMost = true;
		checks.Controls.AddRange(new Control[7] { cbSelfCenter, cbInvertPitch, cbInvertRoll, cbOverlay, cbMoveHud, cbHideCursor, cbTopMost });
		AddRow(t, ref row, checks, SizeType.AutoSize, 0);
		// Global like the HUD position (overlay.cfg), not per profile.
		tbHudSize = AddSliderRow(t, ref row, "HUD size %", OverlayForm.MinSizePercent, OverlayForm.MaxSizePercent, OverlayForm.LoadSizePercent(), autosave: false);
		tbHudSize.ValueChanged += (object? s, EventArgs e) =>
		{
			if (overlay == null || overlay.IsDisposed)
			{
				overlay = new OverlayForm();
			}
			overlay.SetSizePercent(tbHudSize.Value);
		};
		lblHotkeyHelp = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(430, 0),
			ForeColor = Color.Gray,
			Margin = new Padding(2, 8, 2, 2)
		};
		AddRow(t, ref row, lblHotkeyHelp, SizeType.AutoSize, 0, fill: false);
	}

	private void BuildKeysTab(Control host)
	{
		TableLayoutPanel t = NewColumn();
		host.Controls.Add(t);
		int row = 0;
		AddRow(t, ref row, new Label
		{
			AutoSize = true,
			MaximumSize = new Size(430, 0),
			ForeColor = Color.Silver,
			Text = "Map any key to a vJoy button, then bind that button in MSFS (gear, flaps, brakes, AP...). Saved per aircraft profile."
		}, SizeType.AutoSize, 0, fill: false);
		FlowLayoutPanel addBar = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = false,
			Margin = new Padding(0, 6, 0, 4)
		};
		Button btnAdd = Styled("+  Add key → button", Color.FromArgb(45, 70, 110));
		btnAdd.AutoSize = true;
		btnAdd.Padding = new Padding(8, 4, 8, 4);
		btnAdd.Click += (object? s, EventArgs e) =>
		{
			BeginAddBinding();
		};
		addBar.Controls.Add(btnAdd);
		lblBindStatus = new Label
		{
			ForeColor = Color.DarkGray,
			AutoSize = true,
			Margin = new Padding(10, 9, 0, 0)
		};
		addBar.Controls.Add(lblBindStatus);
		AddRow(t, ref row, addBar, SizeType.AutoSize, 0, fill: false);
		bindPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = Color.FromArgb(24, 24, 30),
			Margin = new Padding(2, 2, 2, 6)
		};
		AddRow(t, ref row, bindPanel, SizeType.Percent, 100);
		cbRudder = new CheckBox
		{
			Text = "Keyboard rudder → vJoy Z axis (keys on the Hotkeys tab)",
			ForeColor = ForeColor,
			AutoSize = true,
			Margin = new Padding(2, 6, 2, 4)
		};
		cbRudder.CheckedChanged += (object? s, EventArgs e) =>
		{
			if (!loading)
			{
				profile.RudderEnabled = cbRudder.Checked;
				SaveActiveProfile();
			}
		};
		AddRow(t, ref row, cbRudder, SizeType.AutoSize, 0, fill: false);
		tbRudderRate = AddSliderRow(t, ref row, "Rudder ramp / return speed (%/sec)", 10, 200, 60);
	}

	private void BuildHotkeysTab(Control host)
	{
		TableLayoutPanel t = NewColumn();
		host.Controls.Add(t);
		int row = 0;
		AddRow(t, ref row, new Label
		{
			AutoSize = true,
			MaximumSize = new Size(430, 0),
			ForeColor = Color.Silver,
			Text = "Click a key to change it, then press the new key. Esc cancels. × or Backspace removes the key, turning the command off."
		}, SizeType.AutoSize, 0, fill: false);
		string group = null;
		foreach (Hotkey hk in Hotkey.All)
		{
			if (hk.Group != group)
			{
				group = hk.Group;
				AddRow(t, ref row, new Label
				{
					Text = group,
					ForeColor = Color.DarkGray,
					AutoSize = true,
					Font = new Font("Segoe UI", 8f, FontStyle.Bold),
					Margin = new Padding(2, 10, 2, 2)
				}, SizeType.AutoSize, 0, fill: false);
			}
			AddRow(t, ref row, BuildHotkeyRow(hk), SizeType.AutoSize, 0);
		}
		AddRow(t, ref row, new Panel
		{
			BackColor = Bg
		}, SizeType.Percent, 100);
		TableLayoutPanel bottom = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = new Padding(2, 6, 2, 2),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bottom.Controls.Add(new Label
		{
			Text = "Saved with the current aircraft profile.",
			ForeColor = Color.DarkGray,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		}, 0, 0);
		Button restore = Styled("Restore defaults", Color.FromArgb(45, 55, 75));
		restore.AutoSize = true;
		restore.Padding = new Padding(8, 3, 8, 3);
		restore.Click += (object? s, EventArgs e) =>
		{
			RestoreDefaultHotkeys();
		};
		bottom.Controls.Add(restore, 1, 0);
		AddRow(t, ref row, bottom, SizeType.AutoSize, 0);
	}

	private Control BuildHotkeyRow(Hotkey hk)
	{
		HotkeyRow r = new HotkeyRow
		{
			Hotkey = hk
		};
		TableLayoutPanel box = new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Color.FromArgb(30, 30, 38),
			Margin = new Padding(2, 1, 2, 1),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		TableLayoutPanel line = new TableLayoutPanel
		{
			ColumnCount = 4,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = new Padding(8, 1, 2, 1),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		line.Controls.Add(new Label
		{
			Text = hk.Label,
			ForeColor = ForeColor,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		}, 0, 0);
		r.Key = Styled("", Color.FromArgb(45, 45, 55));
		r.Key.Width = 140;
		r.Key.Height = 26;
		r.Key.Margin = new Padding(2, 3, 2, 3);
		r.Key.FlatAppearance.BorderColor = Color.FromArgb(240, 140, 60);
		r.Key.Click += (object? s, EventArgs e) =>
		{
			BeginHotkeyCapture(r);
		};
		line.Controls.Add(r.Key, 1, 0);
		r.Clear = Styled("×", Color.FromArgb(30, 30, 38));
		r.Clear.ForeColor = Color.DarkGray;
		r.Clear.Font = new Font("Segoe UI", 11f);
		r.Clear.Width = 28;
		r.Clear.Height = 26;
		r.Clear.Margin = new Padding(2, 3, 2, 3);
		r.Clear.AccessibleName = "Remove the key for " + hk.Label;
		r.Clear.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			AssignHotkey(r, 0);
		};
		line.Controls.Add(r.Clear, 2, 0);
		Button reset = Styled("↺", Color.FromArgb(30, 30, 38));
		reset.ForeColor = Color.DarkGray;
		reset.Width = 28;
		reset.Height = 26;
		reset.Margin = new Padding(2, 3, 2, 3);
		reset.AccessibleName = "Reset " + hk.Label + " to default";
		reset.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			AssignHotkey(r, hk.DefaultVk);
		};
		line.Controls.Add(reset, 3, 0);
		box.Controls.Add(line, 0, 0);
		TableLayoutPanel bar = new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(42, 34, 24),
			Margin = Padding.Empty,
			Padding = new Padding(8, 2, 2, 2),
			Visible = false,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		r.ConflictText = new Label
		{
			ForeColor = Color.FromArgb(242, 210, 176),
			AutoSize = true,
			Anchor = AnchorStyles.Left
		};
		bar.Controls.Add(r.ConflictText, 0, 0);
		Button swap = Styled("Swap", Color.FromArgb(45, 70, 110));
		swap.Size = new Size(60, 24);
		swap.Click += (object? s, EventArgs e) =>
		{
			SwapHotkeyConflict();
		};
		bar.Controls.Add(swap, 1, 0);
		Button cancel = Styled("Cancel", Color.FromArgb(60, 60, 70));
		cancel.Size = new Size(60, 24);
		cancel.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
		};
		bar.Controls.Add(cancel, 2, 0);
		r.ConflictBar = bar;
		box.Controls.Add(bar, 0, 1);
		hotkeyRows.Add(r);
		return box;
	}

	private void BeginHotkeyCapture(HotkeyRow r)
	{
		CancelHotkeyCapture();
		lblBindStatus.Text = "";
		capturingRow = r;
		RefreshHotkeyRows();
		capture = (int vk) =>
		{
			capturingRow = null;
			if (vk == (int)Keys.Escape)
			{
				RefreshHotkeyRows();
				return;
			}
			AssignHotkey(r, (vk == (int)Keys.Back) ? 0 : vk);
		};
	}

	private void CancelHotkeyCapture()
	{
		if (capturingRow != null)
		{
			capturingRow = null;
			capture = null;
		}
		conflictRow = null;
		conflictWith = null;
		RefreshHotkeyRows();
	}

	// A key already used by another command isn't applied directly: the row shows the clash
	// and the user picks Swap (the other command takes this one's old key) or Cancel.
	private void AssignHotkey(HotkeyRow r, int vk)
	{
		conflictRow = null;
		conflictWith = null;
		if (vk != 0)
		{
			foreach (Hotkey other in Hotkey.All)
			{
				if (other != r.Hotkey && other.Get(profile) == vk)
				{
					conflictRow = r;
					conflictVk = vk;
					conflictWith = other;
					RefreshHotkeyRows();
					return;
				}
			}
		}
		r.Hotkey.Set(profile, vk);
		HotkeysChanged();
	}

	private void SwapHotkeyConflict()
	{
		if (conflictRow != null)
		{
			conflictWith.Set(profile, conflictRow.Hotkey.Get(profile));
			conflictRow.Hotkey.Set(profile, conflictVk);
			conflictRow = null;
			conflictWith = null;
			HotkeysChanged();
		}
	}

	private void RestoreDefaultHotkeys()
	{
		if (MessageBox.Show("Restore the default hotkeys for “" + profile.Name + "”?", "MouseYokeEnhanced", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			CancelHotkeyCapture();
			foreach (Hotkey hk in Hotkey.All)
			{
				hk.Set(profile, hk.DefaultVk);
			}
			HotkeysChanged();
		}
	}

	private void HotkeysChanged()
	{
		ReleaseHeldKeys();
		SaveActiveProfile();
		RefreshHotkeyUi();
	}

	// A held key's key-up is matched against the current bindings, so after a rebind or a
	// profile switch it may never match again: drop every held state instead.
	private void ReleaseHeldKeys()
	{
		trimUpHeld = (trimDnHeld = (trimLHeld = (trimRHeld = false)));
		profile.RudderLeftHeld = false;
		profile.RudderRightHeld = false;
	}

	private void RefreshHotkeyRows()
	{
		foreach (HotkeyRow r in hotkeyRows)
		{
			bool capturing = r == capturingRow;
			bool conflict = r == conflictRow;
			r.Key.Text = (capturing ? "Press a key…" : Profile.KeyName(conflict ? conflictVk : r.Hotkey.Get(profile)));
			r.Key.BackColor = (capturing ? Color.FromArgb(45, 70, 110) : (conflict ? Color.FromArgb(58, 44, 30) : Color.FromArgb(45, 45, 55)));
			r.Key.FlatAppearance.BorderSize = (conflict ? 1 : 0);
			r.Clear.Enabled = r.Hotkey.Get(profile) != 0;
			if (conflict)
			{
				r.ConflictText.Text = "⚠  " + Profile.KeyName(conflictVk) + " is already " + conflictWith.Label + ".";
			}
			r.ConflictBar.Visible = conflict;
		}
	}

	private void RefreshHotkeyUi()
	{
		RefreshHotkeyRows();
		lblHotkeyHelp.Text = Profile.KeyName(profile.ToggleVk) + " = fly / standby   •   " + Profile.KeyName(profile.TrimUpVk) + "/" + Profile.KeyName(profile.TrimDownVk) + " = pitch trim   •   " + Profile.KeyName(profile.TrimLeftVk) + "/" + Profile.KeyName(profile.TrimRightVk) + " = roll trim   •   " + Profile.KeyName(profile.TrimResetVk) + " = reset trim";
		resetHint = ((profile.TrimResetVk == 0) ? "" : ("   (" + Profile.KeyName(profile.TrimResetVk) + " = reset)"));
		UpdateToggleText();
	}

	private string ToggleKeyHint()
	{
		if (profile.ToggleVk != 0)
		{
			return "  (" + Profile.KeyName(profile.ToggleVk) + ")";
		}
		return "";
	}

	private void UpdateToggleText()
	{
		btnToggle.Text = (active ? "ACTIVE — Deactivate" : "Activate") + ToggleKeyHint();
	}

	private void RefreshProfileCombo()
	{
		switching = true;
		cbProfiles.Items.Clear();
		foreach (string n in ProfileManager.List())
		{
			cbProfiles.Items.Add(n);
		}
		if (cbProfiles.Items.Contains(profile.Name))
		{
			cbProfiles.SelectedItem = profile.Name;
		}
		else if (cbProfiles.Items.Count > 0)
		{
			cbProfiles.SelectedIndex = 0;
		}
		switching = false;
	}

	private void ApplyProfileToUi()
	{
		loading = true;
		tbRollSens.Value = Clamp(profile.RollSens, 1, 100);
		tbPitchSens.Value = Clamp(profile.PitchSens, 1, 100);
		tbDeadzone.Value = Clamp(profile.Deadzone, 0, 30);
		tbExpo.Value = Clamp(profile.Expo, 0, 90);
		tbSmooth.Value = Clamp(profile.Smoothing, 0, 80);
		tbCenterSpeed.Value = Clamp(profile.CenterSpeed, 10, 300);
		tbTrimRate.Value = Clamp(profile.TrimRate, 1, 100);
		cbInvertPitch.Checked = profile.InvertPitch;
		cbInvertRoll.Checked = profile.InvertRoll;
		cbHideCursor.Checked = profile.HideCursor;
		cbSelfCenter.Checked = profile.SelfCenter;
		cbRudder.Checked = profile.RudderEnabled;
		tbRudderRate.Value = Clamp(profile.RudderRatePct, 10, 200);
		RebuildBindingRows();
		ReleaseHeldKeys();
		CancelHotkeyCapture();
		RefreshHotkeyUi();
		loading = false;
	}

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
			profile.HideCursor = cbHideCursor.Checked;
			profile.SelfCenter = cbSelfCenter.Checked;
			profile.RudderEnabled = cbRudder.Checked;
			profile.RudderRatePct = tbRudderRate.Value;
			ProfileManager.Save(profile);
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
		profile.RudderValue = 0.0;
		if (acquired)
		{
			VJoy.SetAxis(16384, 1u, 50u);
		}
		ProfileManager.Save(profile);
		profile = next;
		ProfileManager.SetActive(profile.Name);
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Button > vjoyButtonCount)
			{
				b.Button = vjoyButtonCount;
			}
		}
		ApplyProfileToUi();
		SetStatus("Loaded profile “" + profile.Name + "”.", Color.LightGreen);
	}

	private void NewProfile()
	{
		string name = Prompt("New profile", "Aircraft / profile name:", "New Aircraft");
		if (!string.IsNullOrWhiteSpace(name))
		{
			name = UniqueName(ProfileManager.Sanitize(name));
			Profile p = new Profile
			{
				Name = name
			};
			ProfileManager.Save(p);
			ProfileManager.Save(profile);
			profile = p;
			ProfileManager.SetActive(name);
			RefreshProfileCombo();
			ApplyProfileToUi();
		}
	}

	private void DuplicateProfile()
	{
		string name = Prompt("Duplicate profile", "Name for the copy:", profile.Name + " copy");
		if (!string.IsNullOrWhiteSpace(name))
		{
			name = UniqueName(ProfileManager.Sanitize(name));
			SaveActiveProfile();
			Profile p = ProfileManager.Clone(profile, name);
			ProfileManager.Save(p);
			profile = p;
			ProfileManager.SetActive(name);
			RefreshProfileCombo();
			ApplyProfileToUi();
		}
	}

	private void RenameProfile()
	{
		string name = Prompt("Rename profile", "New name:", profile.Name);
		if (!string.IsNullOrWhiteSpace(name))
		{
			name = ProfileManager.Sanitize(name);
			if (!(name == profile.Name))
			{
				name = UniqueName(name);
				string name2 = profile.Name;
				profile.Name = name;
				ProfileManager.Save(profile);
				ProfileManager.Delete(name2);
				ProfileManager.SetActive(name);
				RefreshProfileCombo();
			}
		}
	}

	private void DeleteProfile()
	{
		if (ProfileManager.List().Count <= 1)
		{
			MessageBox.Show("You need at least one profile.", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (MessageBox.Show("Delete profile “" + profile.Name + "”?", "MouseYokeEnhanced", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
		{
			ReleaseAllButtons();
			ProfileManager.Delete(profile.Name);
			List<string> remaining = ProfileManager.List();
			profile = ProfileManager.Load(remaining[0]) ?? new Profile
			{
				Name = remaining[0]
			};
			ProfileManager.SetActive(profile.Name);
			RefreshProfileCombo();
			ApplyProfileToUi();
		}
	}

	private string UniqueName(string name)
	{
		if (!ProfileManager.Exists(name) || name == profile.Name)
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

	private string Prompt(string title, string label, string defaultValue)
	{
		using Form dlg = new Form();
		dlg.Text = title;
		dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
		dlg.StartPosition = FormStartPosition.CenterParent;
		dlg.TopMost = true;
		dlg.ShowInTaskbar = false;
		dlg.ClientSize = new Size(340, 120);
		dlg.MaximizeBox = false;
		dlg.MinimizeBox = false;
		dlg.BackColor = Color.FromArgb(28, 28, 36);
		dlg.ForeColor = Color.Gainsboro;
		dlg.Font = Font;
		dlg.Controls.Add(new Label
		{
			Text = label,
			Bounds = new Rectangle(14, 14, 312, 20),
			AutoSize = false
		});
		TextBox tb = new TextBox
		{
			Text = defaultValue,
			Bounds = new Rectangle(14, 38, 312, 24),
			BackColor = Color.FromArgb(45, 45, 55),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		dlg.Controls.Add(tb);
		Button ok = new Button
		{
			Text = "OK",
			DialogResult = DialogResult.OK,
			Bounds = new Rectangle(150, 78, 80, 28),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(45, 90, 70),
			ForeColor = Color.White
		};
		Button cancel = new Button
		{
			Text = "Cancel",
			DialogResult = DialogResult.Cancel,
			Bounds = new Rectangle(240, 78, 80, 28),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(60, 60, 70),
			ForeColor = Color.White
		};
		dlg.Controls.Add(ok);
		dlg.Controls.Add(cancel);
		dlg.AcceptButton = ok;
		dlg.CancelButton = cancel;
		tb.SelectAll();
		return (dlg.ShowDialog(this) == DialogResult.OK) ? tb.Text : null;
	}

	private void BeginAddBinding()
	{
		CancelHotkeyCapture();
		lblBindStatus.Text = "Press the key to bind...";
		lblBindStatus.ForeColor = Color.LightGreen;
		capture = (int vk) =>
		{
			lblBindStatus.Text = "";
			int num = profile.Buttons.Count + 1;
			if (num > vjoyButtonCount)
			{
				num = vjoyButtonCount;
			}
			profile.Buttons.Add(new ButtonBinding
			{
				Vk = vk,
				Button = num
			});
			SaveActiveProfile();
			RebuildBindingRows();
		};
	}

	private void RebuildBindingRows()
	{
		bindPanel.SuspendLayout();
		bindPanel.Controls.Clear();
		bindRows.Clear();
		foreach (ButtonBinding b in profile.Buttons)
		{
			FlowLayoutPanel rowP = new FlowLayoutPanel
			{
				AutoSize = true,
				WrapContents = false,
				FlowDirection = FlowDirection.LeftToRight,
				Margin = new Padding(3, 3, 3, 0),
				BackColor = Color.FromArgb(30, 30, 38)
			};
			Panel lamp = new Panel
			{
				Width = 14,
				Height = 14,
				Margin = new Padding(6, 9, 6, 9),
				BackColor = Color.FromArgb(70, 70, 80)
			};
			rowP.Controls.Add(lamp);
			Button keyBtn = Styled(Profile.KeyName(b.Vk), Color.FromArgb(45, 45, 55));
			keyBtn.AutoSize = false;
			keyBtn.Width = 150;
			keyBtn.Height = 26;
			keyBtn.Margin = new Padding(2, 3, 2, 3);
			keyBtn.Tag = b;
			keyBtn.Click += (object? s, EventArgs e) =>
			{
				ButtonBinding bb = (ButtonBinding)((Button)s).Tag;
				CancelHotkeyCapture();
				lblBindStatus.Text = "Press new key...";
				capture = (int vk) =>
				{
					bb.Vk = vk;
					SaveActiveProfile();
					RebuildBindingRows();
					lblBindStatus.Text = "";
				};
			};
			rowP.Controls.Add(keyBtn);
			rowP.Controls.Add(new Label
			{
				Text = "→ btn",
				ForeColor = Color.Silver,
				AutoSize = true,
				Margin = new Padding(4, 8, 2, 0)
			});
			NumericUpDown num = new NumericUpDown
			{
				Width = 56,
				Minimum = 1m,
				Maximum = vjoyButtonCount,
				Value = Math.Min(Math.Max(b.Button, 1), vjoyButtonCount),
				BackColor = Color.FromArgb(45, 45, 55),
				ForeColor = Color.White,
				Margin = new Padding(2, 4, 2, 3),
				Tag = b
			};
			num.ValueChanged += (object? s, EventArgs e) =>
			{
				ButtonBinding buttonBinding = (ButtonBinding)((NumericUpDown)s).Tag;
				if (acquired)
				{
					VJoy.SetBtn(Value: false, 1u, (byte)buttonBinding.Button);
				}
				buttonBinding.Button = (int)((NumericUpDown)s).Value;
				SaveActiveProfile();
			};
			rowP.Controls.Add(num);
			Button del = Styled("Remove", Color.FromArgb(90, 40, 40));
			del.AutoSize = false;
			del.Width = 84;
			del.Height = 26;
			del.Margin = new Padding(8, 3, 2, 3);
			del.Tag = b;
			del.Click += (object? s, EventArgs e) =>
			{
				ButtonBinding buttonBinding = (ButtonBinding)((Button)s).Tag;
				if (acquired)
				{
					VJoy.SetBtn(Value: false, 1u, (byte)buttonBinding.Button);
				}
				profile.Buttons.Remove(buttonBinding);
				SaveActiveProfile();
				RebuildBindingRows();
			};
			rowP.Controls.Add(del);
			bindPanel.Controls.Add(rowP);
			bindRows.Add((b, lamp));
		}
		bindPanel.ResumeLayout();
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
		EnsureOverlay();
		if (active)
		{
			Rectangle b = Screen.PrimaryScreen.Bounds;
			flightScreen = b;
			ConfineCursor();
			SetCursorPos(b.Left + b.Width / 2, b.Top + b.Height / 2);
			smoothX = (smoothY = 0.0);
			springX = (springY = 0.0);
			rawDX = (rawDY = 0);
			if (cbHideCursor.Checked)
			{
				CursorHider.Hide();
			}
			if (overlay != null)
			{
				overlay.Visible = cbOverlay.Checked;
			}
			UpdateToggleText();
			btnToggle.BackColor = Color.FromArgb(150, 40, 40);
			SetStatus("Flying. Mouse = yoke. Trim holds attitude." + ((profile.ToggleVk != 0) ? (" " + Profile.KeyName(profile.ToggleVk) + " to release.") : ""), Color.LightGreen);
		}
		else
		{
			CursorHider.Restore();
			ReleaseCursor();
			if (overlay != null && !cbMoveHud.Checked)
			{
				overlay.Visible = false;
			}
			UpdateToggleText();
			btnToggle.BackColor = Color.FromArgb(40, 120, 90);
			SetStatus("Standby. Trim holds; mouse free for cockpit.", Color.DarkGray);
		}
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

	private void EnsureOverlay()
	{
		if (overlay == null || overlay.IsDisposed)
		{
			overlay = new OverlayForm();
			overlay.Show();
		}
		overlay.Visible = cbOverlay.Checked && (active || cbMoveHud.Checked);
		if (cbMoveHud != null && cbMoveHud.Checked)
		{
			overlay.SetMovable(on: true);
		}
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
		if (active)
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
		double mRoll = 0.0;
		double mPitch = 0.0;
		if (active && cbSelfCenter.Checked)
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
				if (cbHideCursor.Checked)
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
			mRoll = ox;
			mPitch = oy;
		}
		else if (active)
		{
			rawDX = 0;
			rawDY = 0;
			Rectangle scr2 = flightScreen;
			double cx = (double)scr2.Left + (double)scr2.Width / 2.0;
			double cy = (double)scr2.Top + (double)scr2.Height / 2.0;
			double zoneRoll = 1.0 - (double)(tbRollSens.Value - 1) / 99.0 * 0.8;
			double zonePitch = 1.0 - (double)(tbPitchSens.Value - 1) / 99.0 * 0.8;
			double nx = Clamp(((double)System.Windows.Forms.Cursor.Position.X - cx) / ((double)scr2.Width / 2.0 * zoneRoll), -1.0, 1.0);
			double ny = Clamp(((double)System.Windows.Forms.Cursor.Position.Y - cy) / ((double)scr2.Height / 2.0 * zonePitch), -1.0, 1.0);
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
			mRoll = smoothX;
			mPitch = smoothY;
		}
		else
		{
			smoothX = (smoothY = 0.0);
			springX = (springY = 0.0);
			rawDX = 0;
			rawDY = 0;
		}
		double outRoll = Clamp(mRoll + rollTrim, -1.0, 1.0);
		double outPitch = Clamp(mPitch + pitchTrim, -1.0, 1.0);
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
		lblAxis.Text = $"Roll {outRoll,6:0.00}   Pitch {outPitch,6:0.00}" + (profile.RudderEnabled ? $"   Rudder {profile.RudderValue,5:0.00}" : "");
		lblTrim.Text = $"Pitch trim {pitchTrim * 100.0,5:0.#}%   Roll trim {rollTrim * 100.0,5:0.#}%" + resetHint;
		if (++frame % 3 != 0)
		{
			return;
		}
		if (overlay != null && overlay.Visible)
		{
			overlay.UpdateValues(outRoll, outPitch, pitchTrim, rollTrim, active);
		}
		foreach (var bindRow in bindRows)
		{
			ButtonBinding b = bindRow.b;
			Panel lamp = bindRow.lamp;
			Color c = (b.Pressed ? Color.FromArgb(120, 255, 140) : Color.FromArgb(70, 70, 80));
			if (lamp.BackColor != c)
			{
				lamp.BackColor = c;
			}
		}
	}

	private static double Shape(double x, double dz, double expo)
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

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		CursorHider.Restore();
		ReleaseCursor();
		SaveActiveProfile();
		hook.Dispose();
		if (acquired)
		{
			VJoy.ResetVJD(1u);
			VJoy.RelinquishVJD(1u);
		}
		overlay?.Close();
		base.OnFormClosing(e);
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

	[DllImport("user32.dll")]
	private static extern uint GetRawInputData(nint hRawInput, uint uiCommand, nint pData, ref uint pcbSize, uint cbSizeHeader);

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
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
