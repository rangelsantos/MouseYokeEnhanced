using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MouseYokeVJoy;

// The screens: a fixed header (profile, vJoy status, Activate), four tabs (Fly, Feel, Keys, HUD),
// a status bar, and the compact window. All built in code; there is no designer.
public partial class MainForm
{
	private sealed class HotkeyRow
	{
		public Hotkey Hotkey;

		public Label Name;

		public Button Key;

		public Button Clear;

		public Control ConflictBar;

		public Label ConflictText;

		public Label Warn;
	}

	private sealed class BindRow
	{
		public ButtonBinding B;

		public Panel Lamp;

		public Label Warn;
	}

	private sealed class SliderRow
	{
		public Caption Caption;

		public Label Value;

		public string Unit;
	}

	private const int FullWidth = 480;

	private const int FullHeight = 720;

	private const int CompactWidth = 420;

	private const int CompactBodyHeight = 88;

	private const int StatusHeight = 28;

	private static readonly int[] HeaderRows = new int[3] { 46, 56, 42 };

	private ToolTip toolTip;

	private NotifyIcon tray;

	private TableLayoutPanel root;

	private Control[] headerParts;

	private Panel pageHost;

	private readonly TabButton[] tabButtons = new TabButton[4];

	private readonly Control[] pages = new Control[4];

	private int selectedTab;

	private ProfileButton profileButton;

	private Button moreButton;

	private StatusChip vjoyChip;

	private ActivateButton btnToggle;

	private Label lblStatus;

	private Label lblSaved;

	private StickMonitor monitor;

	private Label lblFlyMode;

	private Label lblRoll;

	private Label lblPitch;

	private Label lblRudder;

	private Label lblPitchTrim;

	private Label lblRollTrim;

	private LampStrip lamps;

	private ToggleSwitch swHideCursor;

	private ToggleSwitch swShowHudFly;

	private KeyLegend legend;

	private Segmented segMode;

	private Label lblModeHelp;

	private CurveView curve;

	private Slider tbRollSens;

	private Slider tbPitchSens;

	private Slider tbExpo;

	private Slider tbDeadzone;

	private Slider tbSmooth;

	private Slider tbCenterSpeed;

	private Slider tbTrimRate;

	private CheckBox cbInvertRoll;

	private CheckBox cbInvertPitch;

	private Segmented segKeys;

	private Control keysCommands;

	private Control keysButtons;

	private ToggleSwitch swRudder;

	private Slider tbRudderRate;

	private readonly List<HotkeyRow> hotkeyRows = new List<HotkeyRow>();

	private HotkeyRow capturingRow;

	private HotkeyRow conflictRow;

	private int conflictVk;

	private Hotkey conflictWith;

	private FlowLayoutPanel bindPanel;

	private Label lblBindStatus;

	private Label lblBindCount;

	private readonly List<BindRow> bindRows = new List<BindRow>();

	private HudPreview hudPreview;

	private Segmented segStyle;

	private Slider tbHudSize;

	private Slider tbHudOpacity;

	private ToggleSwitch swShowHud;

	private ToggleSwitch swHudNumbers;

	private ToggleSwitch swHudFade;

	private ToggleSwitch swHudStandby;

	private Button btnPositionHud;

	private ToggleSwitch swTopMost;

	private ToggleSwitch swTray;

	private Control compactPanel;

	private StickMonitor compactMonitor;

	private Label lblCompactState;

	private Label lblCompactProfile;

	private Label lblCompactTrim;

	private bool compact;

	private void BuildUi()
	{
		toolTip = new ToolTip
		{
			InitialDelay = 400
		};
		root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 6,
			BackColor = Theme.Bg,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		foreach (int h in HeaderRows)
		{
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
		}
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
		root.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusHeight));
		Controls.Add(root);
		Control appBar = BuildAppBar();
		Control activateRow = BuildActivateRow();
		Control tabBar = BuildTabBar();
		headerParts = new Control[3] { appBar, activateRow, tabBar };
		root.Controls.Add(appBar, 0, 0);
		root.Controls.Add(activateRow, 0, 1);
		root.Controls.Add(tabBar, 0, 2);
		pageHost = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Margin = Padding.Empty
		};
		root.Controls.Add(pageHost, 0, 3);
		pages[0] = BuildFlyPage();
		pages[1] = BuildFeelPage();
		pages[2] = BuildKeysPage();
		pages[3] = BuildHudPage();
		foreach (Control p in pages)
		{
			p.Visible = false;
			pageHost.Controls.Add(p);
		}
		compactPanel = BuildCompact();
		compactPanel.Visible = false;
		root.Controls.Add(compactPanel, 0, 4);
		root.Controls.Add(BuildStatusBar(), 0, 5);
		BuildTray();
		SelectTab(0);
	}

	// ---------- building blocks ----------

	private TableLayoutPanel NewColumn()
	{
		return new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			AutoScroll = true,
			Padding = new Padding(12, 8, 12, 8),
			BackColor = Theme.Bg,
			GrowStyle = TableLayoutPanelGrowStyle.AddRows,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
	}

	// A rounded-looking group: the Row color on the page background, with padding.
	private TableLayoutPanel Card(int padding = 10)
	{
		return new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			Padding = new Padding(padding),
			GrowStyle = TableLayoutPanelGrowStyle.AddRows,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
	}

	private void AddRow(TableLayoutPanel t, ref int row, Control c, SizeType type, int size, bool fill = true, int gap = 8)
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
			c.Margin = new Padding(0, 0, 0, gap);
		}
		t.Controls.Add(c, 0, row);
		row++;
	}

	// Puts a column in a scrolling panel. The column sizes itself to its content (so the last row doesn't
	// stretch to the bottom) and the panel scrolls when that is taller than the window; AutoScroll on a
	// TableLayoutPanel with Percent rows doesn't show a scrollbar reliably.
	private static Panel Scroller(TableLayoutPanel t)
	{
		t.AutoScroll = false;
		t.AutoSize = true;
		t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		t.Dock = DockStyle.Top;
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = t.BackColor,
			Margin = Padding.Empty
		};
		p.Controls.Add(t);
		return p;
	}

	private static Label HeaderLabel(string text)
	{
		return new Label
		{
			Text = text.ToUpperInvariant(),
			Font = Theme.Header,
			ForeColor = Theme.Muted,
			AutoSize = true
		};
	}

	// A group header: an uppercase label, with an optional control on the right.
	private static Control Header(string text, Control right = null)
	{
		if (right == null)
		{
			return HeaderLabel(text);
		}
		TableLayoutPanel t = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		Label l = HeaderLabel(text);
		l.Anchor = AnchorStyles.Left;
		l.Margin = Padding.Empty;
		right.Anchor = AnchorStyles.Right;
		right.Margin = Padding.Empty;
		t.Controls.Add(l, 0, 0);
		t.Controls.Add(right, 1, 0);
		return t;
	}

	// One line: caption (with an optional hint), slider, value with unit. The row matches its parent's color.
	private Slider AddSliderRow(TableLayoutPanel t, ref int row, string label, string hint, int min, int max, int val, string unit, bool autosave = true)
	{
		TableLayoutPanel line = new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = t.BackColor,
			Margin = new Padding(0, 0, 0, 4),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 118f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 58f)
			}
		};
		Caption caption = new Caption
		{
			Text = label,
			Hint = hint,
			BackColor = t.BackColor,
			Dock = DockStyle.Fill,
			Margin = Padding.Empty
		};
		Slider s = new Slider
		{
			Minimum = min,
			Maximum = max,
			Value = val,
			DefaultValue = val,
			BackColor = t.BackColor,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = new Padding(4, 0, 6, 0),
			AccessibleName = label
		};
		Label value = new Label
		{
			Text = val + unit,
			AutoSize = false,
			Size = new Size(58, 22),
			TextAlign = ContentAlignment.MiddleRight,
			BackColor = Theme.Input,
			ForeColor = Color.White,
			Padding = new Padding(0, 0, 4, 0),
			Anchor = AnchorStyles.Right,
			Margin = Padding.Empty
		};
		s.Tag = new SliderRow
		{
			Caption = caption,
			Value = value,
			Unit = unit
		};
		s.ValueChanged += (object? o, EventArgs e) =>
		{
			value.Text = s.Value + unit;
		};
		if (autosave)
		{
			s.ValueChanged += (object? o, EventArgs e) =>
			{
				SaveActiveProfile();
			};
		}
		toolTip.SetToolTip(s, "Double-click to reset to " + val + unit);
		line.Controls.Add(caption, 0, 0);
		line.Controls.Add(s, 1, 0);
		line.Controls.Add(value, 2, 0);
		AddRow(t, ref row, line, SizeType.AutoSize, 0, fill: true, 4);
		return s;
	}

	private static void SetRowEnabled(Slider s, bool on)
	{
		s.Enabled = on;
		if (s.Tag is SliderRow r)
		{
			r.Caption.Dim = !on;
			r.Value.ForeColor = (on ? Color.White : Theme.Faint);
		}
	}

	private Button Styled(string text, Color back)
	{
		Button button = new Button();
		button.Text = text;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = back;
		button.ForeColor = Color.White;
		button.AutoSize = false;
		button.Font = Theme.Body;
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15f);
		return button;
	}

	private Button ActionButton(string text, Color back)
	{
		Button b = Styled(text, back);
		b.AutoSize = true;
		b.Height = 28;
		b.Padding = new Padding(8, 1, 8, 1);
		b.Margin = new Padding(0, 0, 6, 0);
		return b;
	}

	private ToggleSwitch Switch(string text, string subtitle, EventHandler changed)
	{
		ToggleSwitch sw = new ToggleSwitch
		{
			Text = text,
			Subtitle = subtitle,
			Dock = DockStyle.Fill
		};
		sw.CheckedChanged += changed;
		return sw;
	}

	// Two switches side by side.
	private static TableLayoutPanel SwitchPair(ToggleSwitch a, ToggleSwitch b)
	{
		TableLayoutPanel t = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 50f),
				new ColumnStyle(SizeType.Percent, 50f)
			}
		};
		a.Margin = new Padding(0, 0, 3, 0);
		b.Margin = new Padding(3, 0, 0, 0);
		t.Controls.Add(a, 0, 0);
		t.Controls.Add(b, 1, 0);
		return t;
	}

	private static void SetText(Label l, string text)
	{
		if (l.Text != text)
		{
			l.Text = text;
		}
	}

	// ---------- header, tabs, status bar ----------

	private Control BuildAppBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			BackColor = Theme.Bar,
			Padding = new Padding(10, 8, 10, 8),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 38f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		profileButton = new ProfileButton
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 0, 6, 0)
		};
		profileButton.Click += (object? s, EventArgs e) =>
		{
			ShowProfileMenu();
		};
		moreButton = Styled("⋯", Theme.Input);
		moreButton.Font = new Font("Segoe UI", 11f);
		moreButton.Dock = DockStyle.Fill;
		moreButton.Margin = new Padding(0, 0, 6, 0);
		moreButton.AccessibleName = "Profile actions";
		toolTip.SetToolTip(moreButton, "Profile actions");
		moreButton.Click += (object? s, EventArgs e) =>
		{
			ShowMoreMenu();
		};
		vjoyChip = new StatusChip
		{
			Text = "vJoy…",
			Anchor = AnchorStyles.Right,
			Margin = Padding.Empty
		};
		vjoyChip.Click += (object? s, EventArgs e) =>
		{
			MessageBox.Show(this, vjoyDetails, "vJoy status", MessageBoxButtons.OK, (vjoyChip.Kind == ChipKind.Ok) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
		};
		bar.Controls.Add(profileButton, 0, 0);
		bar.Controls.Add(moreButton, 1, 0);
		bar.Controls.Add(vjoyChip, 2, 0);
		return bar;
	}

	private void SetVjoyChip(string text, ChipKind kind, string details)
	{
		vjoyChip.Text = text;
		vjoyChip.Kind = kind;
		vjoyDetails = details;
		toolTip.SetToolTip(vjoyChip, details);
	}

	private Control BuildActivateRow()
	{
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Padding = new Padding(10, 10, 10, 0),
			Margin = Padding.Empty
		};
		btnToggle = new ActivateButton
		{
			Dock = DockStyle.Fill
		};
		btnToggle.Click += (object? s, EventArgs e) =>
		{
			ToggleActive();
		};
		p.Controls.Add(btnToggle);
		return p;
	}

	private Control BuildTabBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			BackColor = Theme.Bg,
			Padding = new Padding(10, 8, 10, 1),
			Margin = Padding.Empty
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		bar.Paint += (object? s, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Theme.Line);
			e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
		};
		string[] names = new string[4] { "Fly", "Feel", "Keys", "HUD" };
		for (int i = 0; i < 4; i++)
		{
			int index = i;
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
			tabButtons[i] = new TabButton
			{
				Text = names[i],
				Dock = DockStyle.Fill
			};
			tabButtons[i].Click += (object? s, EventArgs e) =>
			{
				SelectTab(index);
			};
			bar.Controls.Add(tabButtons[i], i, 0);
		}
		return bar;
	}

	private void SelectTab(int index)
	{
		selectedTab = index;
		for (int i = 0; i < 4; i++)
		{
			tabButtons[i].Selected = i == index;
			pages[i].Visible = i == index;
		}
	}

	private Control BuildStatusBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			BackColor = Theme.Bar,
			Padding = new Padding(12, 0, 12, 0),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		lblStatus = new Label
		{
			Dock = DockStyle.Fill,
			AutoEllipsis = true,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			Margin = Padding.Empty
		};
		lblSaved = new Label
		{
			Text = "Saved",
			AutoSize = true,
			Anchor = AnchorStyles.Right,
			ForeColor = Theme.Faint,
			Font = Theme.Small,
			Margin = new Padding(8, 0, 0, 0)
		};
		bar.Controls.Add(lblStatus, 0, 0);
		bar.Controls.Add(lblSaved, 1, 0);
		return bar;
	}

	// ---------- profile menus ----------

	// The two profile menus are kept and refilled on every open: an item's Click can open a modal
	// dialog (Rename, Import...), so the menu must still exist when the handler returns.
	private ContextMenuStrip profileMenu;

	private ContextMenuStrip moreMenu;

	private static ContextMenuStrip Refill(ref ContextMenuStrip menu)
	{
		if (menu == null)
		{
			menu = DarkMenuRenderer.NewMenu();
		}
		ToolStripItem[] old = new ToolStripItem[menu.Items.Count];
		menu.Items.CopyTo(old, 0);
		menu.Items.Clear();
		foreach (ToolStripItem item in old)
		{
			item.Dispose();
		}
		return menu;
	}

	private static void ShowMenu(ContextMenuStrip m, Control under)
	{
		m.Show(under, new Point(0, under.Height + 2));
	}

	private void ShowProfileMenu()
	{
		ContextMenuStrip m = Refill(ref profileMenu);
		m.MinimumSize = new Size(profileButton.Width, 0);
		foreach (string n in ProfileManager.List())
		{
			string name = n;
			ToolStripMenuItem item = new ToolStripMenuItem(name)
			{
				Checked = (name == profile.Name)
			};
			item.Click += (object? s, EventArgs e) =>
			{
				SwitchProfile(name);
			};
			m.Items.Add(item);
		}
		ShowMenu(m, profileButton);
	}

	private void ShowMoreMenu()
	{
		ContextMenuStrip m = Refill(ref moreMenu);
		m.Items.Add("New profile…", null, (object? s, EventArgs e) =>
		{
			NewProfile();
		});
		m.Items.Add("Duplicate…", null, (object? s, EventArgs e) =>
		{
			DuplicateProfile();
		});
		m.Items.Add("Rename…", null, (object? s, EventArgs e) =>
		{
			RenameProfile();
		});
		m.Items.Add(new ToolStripSeparator());
		m.Items.Add("Import…", null, (object? s, EventArgs e) =>
		{
			ImportProfile();
		});
		m.Items.Add("Export…", null, (object? s, EventArgs e) =>
		{
			ExportProfile();
		});
		m.Items.Add("Open profiles folder", null, (object? s, EventArgs e) =>
		{
			OpenProfilesFolder();
		});
		m.Items.Add(new ToolStripSeparator());
		ToolStripItem del = m.Items.Add("Delete “" + profile.Name + "”…", null, (object? s, EventArgs e) =>
		{
			DeleteProfile();
		});
		del.Tag = "danger";
		ShowMenu(m, moreButton);
	}

	private void RefreshProfileUi()
	{
		profileButton.ProfileName = profile.Name;
		lblCompactProfile.Text = profile.Name;
		string tip = "MouseYokeEnhanced — " + profile.Name;
		tray.Text = ((tip.Length > 63) ? tip.Substring(0, 63) : tip);
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
		dlg.BackColor = Theme.Bar;
		dlg.ForeColor = Theme.Text;
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
			BackColor = Theme.Input,
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
		dlg.HandleCreated += (object? s, EventArgs e) =>
		{
			int dark = 1;
			DwmSetWindowAttribute(dlg.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
		};
		tb.SelectAll();
		return (dlg.ShowDialog(this) == DialogResult.OK) ? tb.Text : null;
	}

	// ---------- Fly ----------

	private Control BuildFlyPage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		TableLayoutPanel card = Card();
		int cr = 0;
		lblFlyMode = new Label
		{
			Text = "DIRECT",
			AutoSize = true,
			Font = Theme.Header,
			ForeColor = Theme.Cyan
		};
		AddRow(card, ref cr, Header("Stick output", lblFlyMode), SizeType.AutoSize, 0, fill: true, 6);
		TableLayoutPanel mon = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 212f),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		monitor = new StickMonitor
		{
			Size = new Size(200, 208),
			Margin = new Padding(0, 0, 12, 0)
		};
		mon.Controls.Add(monitor, 0, 0);
		TableLayoutPanel read = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		int rr = 0;
		lblRoll = AddReadout(read, ref rr, "Roll", Color.White);
		lblPitch = AddReadout(read, ref rr, "Pitch", Color.White);
		lblRudder = AddReadout(read, ref rr, "Rudder", Color.White);
		Panel sep = new Panel
		{
			Height = 1,
			BackColor = Theme.Line,
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 5, 0, 5)
		};
		read.Controls.Add(sep, 0, rr);
		read.SetColumnSpan(sep, 2);
		rr++;
		lblPitchTrim = AddReadout(read, ref rr, "Pitch trim", Theme.Amber);
		lblRollTrim = AddReadout(read, ref rr, "Roll trim", Theme.Amber);
		mon.Controls.Add(read, 1, 0);
		AddRow(card, ref cr, mon, SizeType.AutoSize, 0, fill: true, 8);
		AddRow(card, ref cr, Header("vJoy buttons"), SizeType.AutoSize, 0, fill: false, 6);
		lamps = new LampStrip
		{
			BackColor = Theme.Row
		};
		AddRow(card, ref cr, lamps, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, card, SizeType.AutoSize, 0);
		swHideCursor = Switch("Hide cursor", "while flying", (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			if (active)
			{
				if (swHideCursor.Checked)
				{
					CursorHider.Hide();
				}
				else
				{
					CursorHider.Restore();
				}
			}
		});
		swShowHudFly = Switch("Show HUD", "while flying", (object? s, EventArgs e) =>
		{
			SetShowHud(swShowHudFly.Checked);
		});
		AddRow(t, ref row, SwitchPair(swHideCursor, swShowHudFly), SizeType.AutoSize, 0);
		TableLayoutPanel keys = Card();
		int kr = 0;
		LinkLabel change = new LinkLabel
		{
			Text = "change on Keys",
			AutoSize = true,
			Font = Theme.Small,
			LinkColor = Theme.Muted,
			ActiveLinkColor = Color.White,
			VisitedLinkColor = Theme.Muted,
			LinkBehavior = LinkBehavior.HoverUnderline
		};
		change.LinkClicked += (object? s, LinkLabelLinkClickedEventArgs e) =>
		{
			SelectTab(2);
		};
		AddRow(keys, ref kr, Header("Keys in use", change), SizeType.AutoSize, 0, fill: true, 6);
		legend = new KeyLegend
		{
			BackColor = Theme.Row
		};
		AddRow(keys, ref kr, legend, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, keys, SizeType.AutoSize, 0);
		return Scroller(t);
	}

	private static Label AddReadout(TableLayoutPanel t, ref int row, string name, Color valueColor)
	{
		t.Controls.Add(new Label
		{
			Text = name,
			AutoSize = true,
			ForeColor = Theme.Muted,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 3, 0, 3)
		}, 0, row);
		Label v = new Label
		{
			Text = "+0.00",
			AutoSize = false,
			Size = new Size(56, 20),
			TextAlign = ContentAlignment.MiddleRight,
			ForeColor = valueColor,
			Anchor = AnchorStyles.Right,
			Margin = Padding.Empty
		};
		t.Controls.Add(v, 1, row);
		row++;
		return v;
	}

	// ---------- Feel ----------

	private Control BuildFeelPage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		TableLayoutPanel modeCard = Card();
		int mr = 0;
		segMode = new Segmented
		{
			Items = new string[2] { "Direct", "Spring" },
			Text = "Stick mode",
			AccessibleName = "Stick mode"
		};
		segMode.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			springX = (springY = 0.0);
			if (active)
			{
				Rectangle bounds = Screen.PrimaryScreen.Bounds;
				SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
			}
			UpdateModeUi();
		};
		AddRow(modeCard, ref mr, segMode, SizeType.AutoSize, 0, fill: true, 6);
		lblModeHelp = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(430, 0),
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			Margin = Padding.Empty
		};
		AddRow(modeCard, ref mr, lblModeHelp, SizeType.AutoSize, 0, fill: false, 0);
		AddRow(t, ref row, modeCard, SizeType.AutoSize, 0);
		TableLayoutPanel curveCard = Card(6);
		int cr = 0;
		curve = new CurveView
		{
			Height = 90,
			BackColor = Theme.Row
		};
		AddRow(curveCard, ref cr, curve, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, curveCard, SizeType.AutoSize, 0);
		TableLayoutPanel stick = Card();
		int sr = 0;
		AddRow(stick, ref sr, Header("Stick"), SizeType.AutoSize, 0, fill: false, 6);
		tbRollSens = AddSliderRow(stick, ref sr, "Roll sensitivity", null, 1, 100, 55, "");
		tbPitchSens = AddSliderRow(stick, ref sr, "Pitch sensitivity", null, 1, 100, 45, "");
		tbExpo = AddSliderRow(stick, ref sr, "Expo", null, 0, 90, 40, "%");
		tbDeadzone = AddSliderRow(stick, ref sr, "Deadzone", "Direct only", 0, 30, 4, "%");
		tbSmooth = AddSliderRow(stick, ref sr, "Smoothing", "Direct only", 0, 80, 15, "%");
		tbCenterSpeed = AddSliderRow(stick, ref sr, "Auto-center", "Spring only", 10, 300, 120, " %/s");
		tbExpo.ValueChanged += (object? s, EventArgs e) =>
		{
			UpdateCurve();
		};
		tbDeadzone.ValueChanged += (object? s, EventArgs e) =>
		{
			UpdateCurve();
		};
		Control trimHeader = Header("Trim");
		trimHeader.Margin = new Padding(0, 6, 0, 6);
		AddRow(stick, ref sr, trimHeader, SizeType.AutoSize, 0, fill: false);
		tbTrimRate = AddSliderRow(stick, ref sr, "Trim rate", "while a key is held", 1, 100, 25, " %/s");
		FlowLayoutPanel checks = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = true,
			BackColor = Theme.Row,
			Margin = new Padding(0, 4, 0, 0)
		};
		cbInvertRoll = new CheckBox
		{
			Text = "Invert roll",
			ForeColor = Theme.Text,
			AutoSize = true,
			Margin = new Padding(0, 2, 16, 2)
		};
		cbInvertPitch = new CheckBox
		{
			Text = "Invert pitch",
			ForeColor = Theme.Text,
			AutoSize = true,
			Margin = new Padding(0, 2, 16, 2)
		};
		cbInvertRoll.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
		};
		cbInvertPitch.CheckedChanged += (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
		};
		checks.Controls.Add(cbInvertRoll);
		checks.Controls.Add(cbInvertPitch);
		AddRow(stick, ref sr, checks, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, stick, SizeType.AutoSize, 0);
		return Scroller(t);
	}

	// Deadzone and Smoothing do nothing in spring mode and Auto-center does nothing in direct mode.
	private void UpdateModeUi()
	{
		bool spring = SpringMode;
		SetRowEnabled(tbDeadzone, !spring);
		SetRowEnabled(tbSmooth, !spring);
		SetRowEnabled(tbCenterSpeed, spring);
		lblModeHelp.Text = (spring ? "Nudge the mouse and the stick springs back to center, like a sidestick. Try Hide cursor with it." : "The mouse position is the stick position. Bring it back to center yourself.");
		lblFlyMode.Text = (spring ? "SPRING" : "DIRECT");
		UpdateCurve();
	}

	private void UpdateCurve()
	{
		curve.SetCurve(SpringMode ? 0.0 : ((double)tbDeadzone.Value / 100.0), (double)tbExpo.Value / 100.0);
	}

	// ---------- Keys ----------

	private Control BuildKeysPage()
	{
		TableLayoutPanel t = NewColumn();
		t.AutoScroll = false;
		int row = 0;
		segKeys = new Segmented
		{
			Items = new string[2] { "Commands", "vJoy buttons" },
			Text = "Key lists",
			AccessibleName = "Key lists"
		};
		segKeys.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			keysCommands.Visible = segKeys.SelectedIndex == 0;
			keysButtons.Visible = segKeys.SelectedIndex == 1;
		};
		AddRow(t, ref row, segKeys, SizeType.AutoSize, 0);
		Panel host = new Panel
		{
			BackColor = Theme.Bg,
			Margin = Padding.Empty
		};
		keysCommands = BuildCommandsList();
		keysButtons = BuildButtonsList();
		keysButtons.Visible = false;
		host.Controls.Add(keysCommands);
		host.Controls.Add(keysButtons);
		AddRow(t, ref row, host, SizeType.Percent, 100, fill: true, 0);
		return t;
	}

	private Control BuildCommandsList()
	{
		TableLayoutPanel t = NewColumn();
		t.Padding = Padding.Empty;
		int row = 0;
		string group = null;
		foreach (Hotkey hk in Hotkey.All)
		{
			if (hk.Group != group)
			{
				group = hk.Group;
				Control h = Header(group);
				h.Margin = new Padding(0, (row == 0) ? 2 : 6, 0, 4);
				AddRow(t, ref row, h, SizeType.AutoSize, 0, fill: false);
				if (hk.IsRudder)
				{
					swRudder = Switch("Keyboard rudder", "drives the vJoy Z axis", (object? s, EventArgs e) =>
					{
						if (!loading)
						{
							profile.RudderEnabled = swRudder.Checked;
							if (!swRudder.Checked)
							{
								// Otherwise Z would stay wherever the rudder was when it was switched off.
								profile.RudderValue = 0.0;
								profile.RudderLeftHeld = (profile.RudderRightHeld = false);
								if (acquired)
								{
									VJoy.SetAxis(16384, 1u, 50u);
								}
							}
							SaveActiveProfile();
						}
						RefreshHotkeyUi();
					});
					swRudder.Margin = new Padding(0, 0, 0, 3);
					AddRow(t, ref row, swRudder, SizeType.AutoSize, 0, fill: true, 3);
				}
			}
			AddRow(t, ref row, BuildHotkeyRow(hk), SizeType.AutoSize, 0, fill: true, 3);
		}
		tbRudderRate = AddSliderRow(t, ref row, "Ramp / return", "rudder speed", 10, 200, 60, " %/s");
		TableLayoutPanel outer = NewColumn();
		outer.AutoScroll = false;
		outer.Padding = Padding.Empty;
		int orow = 0;
		AddRow(outer, ref orow, Scroller(t), SizeType.Percent, 100, fill: true, 0);
		TableLayoutPanel bottom = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = new Padding(0, 4, 0, 0),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bottom.Controls.Add(new Label
		{
			Text = "Esc cancels · Backspace clears · saved with this profile",
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		}, 0, 0);
		Button restore = ActionButton("Restore defaults", Theme.Primary);
		restore.Margin = Padding.Empty;
		restore.Click += (object? s, EventArgs e) =>
		{
			RestoreDefaultHotkeys();
		};
		bottom.Controls.Add(restore, 1, 0);
		AddRow(outer, ref orow, bottom, SizeType.AutoSize, 0, fill: true, 0);
		return outer;
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
			BackColor = Theme.Row,
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
			Padding = new Padding(10, 1, 2, 1),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		r.Name = new Label
		{
			Text = hk.Label,
			ForeColor = Theme.Text,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		};
		line.Controls.Add(r.Name, 0, 0);
		r.Key = Styled("", Theme.Input);
		r.Key.Width = 124;
		r.Key.Height = 24;
		r.Key.Margin = new Padding(2, 1, 2, 1);
		r.Key.FlatAppearance.BorderColor = Color.FromArgb(240, 140, 60);
		r.Key.AccessibleName = hk.Label + " key";
		r.Key.Click += (object? s, EventArgs e) =>
		{
			BeginHotkeyCapture(r);
		};
		line.Controls.Add(r.Key, 1, 0);
		r.Clear = Styled("×", Theme.Row);
		r.Clear.ForeColor = Theme.Muted;
		r.Clear.Font = new Font("Segoe UI", 11f);
		r.Clear.Width = 26;
		r.Clear.Height = 24;
		r.Clear.Margin = new Padding(2, 1, 2, 1);
		r.Clear.AccessibleName = "Remove the key for " + hk.Label;
		toolTip.SetToolTip(r.Clear, "Remove the key (turns this command off)");
		r.Clear.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			AssignHotkey(r, 0);
		};
		line.Controls.Add(r.Clear, 2, 0);
		Button reset = Styled("↺", Theme.Row);
		reset.ForeColor = Theme.Muted;
		reset.Width = 26;
		reset.Height = 24;
		reset.Margin = new Padding(2, 1, 2, 1);
		reset.AccessibleName = "Reset " + hk.Label + " to default";
		toolTip.SetToolTip(reset, "Back to the default key (" + Profile.KeyName(hk.DefaultVk) + ")");
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
			BackColor = Theme.WarnBg,
			Margin = Padding.Empty,
			Padding = new Padding(10, 2, 2, 2),
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
			ForeColor = Theme.Warn,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		};
		bar.Controls.Add(r.ConflictText, 0, 0);
		Button swap = Styled("Swap", Theme.Blue);
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
		r.Warn = WarnLabel();
		box.Controls.Add(r.Warn, 0, 2);
		hotkeyRows.Add(r);
		return box;
	}

	private static Label WarnLabel()
	{
		return new Label
		{
			AutoSize = true,
			MaximumSize = new Size(420, 0),
			Dock = DockStyle.Fill,
			BackColor = Theme.WarnBg,
			ForeColor = Theme.Warn,
			Font = Theme.Small,
			Padding = new Padding(6, 2, 6, 2),
			Margin = new Padding(8, 0, 4, 3),
			Visible = false
		};
	}

	private void BeginHotkeyCapture(HotkeyRow r)
	{
		CancelHotkeyCapture();
		SetBindStatus("");
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

	// What OnKeyDown does with a key that is both a command and a vJoy button: the command runs and
	// the button doesn't, except for the rudder keys (only while keyboard rudder is on), which do both.
	private Hotkey CommandOn(int vk)
	{
		if (vk == 0)
		{
			return null;
		}
		foreach (Hotkey hk in Hotkey.All)
		{
			if (hk.Get(profile) == vk && (!hk.IsRudder || profile.RudderEnabled))
			{
				return hk;
			}
		}
		return null;
	}

	private string HotkeyWarning(Hotkey hk)
	{
		int vk = hk.Get(profile);
		if (vk == 0 || CommandOn(vk) != hk)
		{
			return null;
		}
		List<string> buttons = new List<string>();
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk)
			{
				buttons.Add("vJoy button " + b.Button + (string.IsNullOrWhiteSpace(b.Label) ? "" : (" (" + b.Label.Trim() + ")")));
			}
		}
		if (buttons.Count == 0)
		{
			return null;
		}
		return "⚠  " + Profile.KeyName(vk) + " is also " + string.Join(", ", buttons) + ". " + (hk.IsRudder ? "Both will fire." : "This command wins; the button won't fire.");
	}

	private string BindingWarning(ButtonBinding b)
	{
		Hotkey hk = CommandOn(b.Vk);
		if (hk == null)
		{
			return null;
		}
		return "⚠  " + Profile.KeyName(b.Vk) + " is also " + hk.Label + ". " + (hk.IsRudder ? "Both will fire." : "The command wins; this button won't fire.");
	}

	private void RefreshHotkeyRows()
	{
		foreach (HotkeyRow r in hotkeyRows)
		{
			bool capturing = r == capturingRow;
			bool conflict = r == conflictRow;
			r.Key.Text = (capturing ? "Press a key…" : Profile.KeyName(conflict ? conflictVk : r.Hotkey.Get(profile)));
			r.Key.BackColor = (capturing ? Theme.Blue : (conflict ? Color.FromArgb(58, 44, 30) : Theme.Input));
			r.Key.ForeColor = ((r.Hotkey.Get(profile) == 0 && !capturing && !conflict) ? Theme.Faint : Color.White);
			r.Key.FlatAppearance.BorderSize = (conflict ? 1 : 0);
			r.Clear.Enabled = r.Hotkey.Get(profile) != 0;
			r.Name.ForeColor = ((r.Hotkey.IsRudder && !profile.RudderEnabled) ? Theme.Faint : Theme.Text);
			if (conflict)
			{
				r.ConflictText.Text = "⚠  " + Profile.KeyName(conflictVk) + " is already " + conflictWith.Label + ".";
			}
			r.ConflictBar.Visible = conflict;
			string warn = (conflict ? null : HotkeyWarning(r.Hotkey));
			r.Warn.Text = warn ?? "";
			r.Warn.Visible = warn != null;
		}
	}

	private void RefreshHotkeyUi()
	{
		RefreshHotkeyRows();
		UpdateBindWarnings();
		btnToggle.KeyText = ((profile.ToggleVk != 0) ? Profile.KeyName(profile.ToggleVk) : "");
		List<List<(string, bool)>> lines = new List<List<(string, bool)>>();
		AddLegend(lines, "Fly / standby", profile.ToggleVk);
		AddLegend(lines, "Pitch trim", profile.TrimUpVk, profile.TrimDownVk);
		List<(string, bool)> roll = new List<(string, bool)>();
		AddLegendItems(roll, "Roll trim", profile.TrimLeftVk, profile.TrimRightVk);
		AddLegendItems(roll, "Reset trim", profile.TrimResetVk);
		if (roll.Count > 0)
		{
			lines.Add(roll);
		}
		AddLegend(lines, profile.RudderEnabled ? "Rudder" : "Rudder (off)", profile.RudderLeftVk, profile.RudderRightVk);
		if (lines.Count == 0)
		{
			lines.Add(new List<(string, bool)> { ("No keys set. Assign them on the Keys tab.", false) });
		}
		legend.SetLines(lines);
		SetRowEnabled(tbRudderRate, profile.RudderEnabled);
	}

	private static void AddLegend(List<List<(string, bool)>> lines, string label, params int[] vks)
	{
		List<(string, bool)> line = new List<(string, bool)>();
		AddLegendItems(line, label, vks);
		if (line.Count > 0)
		{
			lines.Add(line);
		}
	}

	private static void AddLegendItems(List<(string, bool)> line, string label, params int[] vks)
	{
		bool any = false;
		foreach (int vk in vks)
		{
			if (vk != 0)
			{
				line.Add((Profile.KeyName(vk), true));
				any = true;
			}
		}
		if (any)
		{
			line.Add((label, false));
		}
	}

	private Control BuildButtonsList()
	{
		TableLayoutPanel t = NewColumn();
		t.AutoScroll = false;
		t.Padding = Padding.Empty;
		int row = 0;
		TableLayoutPanel top = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		top.Controls.Add(new Label
		{
			Text = "Map a key to a vJoy button, then bind that button in the sim (gear, flaps, brakes, autopilot…). Saved with this profile.",
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			AutoSize = true,
			MaximumSize = new Size(330, 0),
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 0, 8, 0)
		}, 0, 0);
		Button add = ActionButton("+  Add key", Theme.Blue);
		add.Margin = Padding.Empty;
		add.Anchor = AnchorStyles.Right;
		add.Click += (object? s, EventArgs e) =>
		{
			BeginAddBinding();
		};
		top.Controls.Add(add, 1, 0);
		AddRow(t, ref row, top, SizeType.AutoSize, 0, fill: true, 6);
		lblBindStatus = new Label
		{
			AutoSize = true,
			ForeColor = Color.LightGreen,
			Visible = false
		};
		AddRow(t, ref row, lblBindStatus, SizeType.AutoSize, 0, fill: false, 6);
		bindPanel = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			BackColor = Theme.Bg
		};
		bindPanel.Resize += (object? s, EventArgs e) =>
		{
			FitBindRows();
		};
		AddRow(t, ref row, bindPanel, SizeType.Percent, 100, fill: true, 6);
		lblBindCount = new Label
		{
			AutoSize = true,
			ForeColor = Theme.Muted,
			Font = Theme.Small
		};
		AddRow(t, ref row, lblBindCount, SizeType.AutoSize, 0, fill: false, 0);
		return t;
	}

	private void SetBindStatus(string text)
	{
		lblBindStatus.Text = text;
		lblBindStatus.Visible = text.Length > 0;
	}

	private void BeginAddBinding()
	{
		CancelHotkeyCapture();
		SetBindStatus("Press the key to bind…");
		capture = (int vk) =>
		{
			SetBindStatus("");
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
			RefreshHotkeyRows();
		};
	}

	private int BindRowWidth => Math.Max(200, bindPanel.ClientSize.Width - 2);

	private void FitBindRows()
	{
		foreach (Control c in bindPanel.Controls)
		{
			c.MinimumSize = new Size(BindRowWidth, 0);
			c.MaximumSize = new Size(BindRowWidth, 0);
		}
	}

	private void RebuildBindingRows()
	{
		bindPanel.SuspendLayout();
		foreach (Control c in bindPanel.Controls)
		{
			c.Dispose();
		}
		bindPanel.Controls.Clear();
		bindRows.Clear();
		if (profile.Buttons.Count == 0)
		{
			bindPanel.Controls.Add(new Label
			{
				Text = "No buttons yet. Click + Add key, then press the key you want to use.",
				ForeColor = Theme.Muted,
				AutoSize = true,
				MaximumSize = new Size(BindRowWidth, 0),
				Margin = new Padding(0, 16, 0, 0)
			});
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			bindPanel.Controls.Add(BuildBindRow(b));
		}
		FitBindRows();
		bindPanel.ResumeLayout();
		UpdateBindWarnings();
		lblBindCount.Text = profile.Buttons.Count + " of " + vjoyButtonCount + " buttons used. The lamp lights while the key is down.";
		segKeys.SetItem(1, "vJoy buttons · " + profile.Buttons.Count);
	}

	private Control BuildBindRow(ButtonBinding b)
	{
		TableLayoutPanel rowP = new TableLayoutPanel
		{
			ColumnCount = 6,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			Margin = new Padding(0, 0, 0, 3),
			Padding = new Padding(6, 2, 2, 2),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 16f),
				new ColumnStyle(SizeType.Absolute, 100f),
				new ColumnStyle(SizeType.Absolute, 20f),
				new ColumnStyle(SizeType.Absolute, 74f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 30f)
			}
		};
		Panel lamp = new Panel
		{
			Width = 10,
			Height = 10,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(70, 70, 80)
		};
		rowP.Controls.Add(lamp, 0, 0);
		Button keyBtn = Styled(Profile.KeyName(b.Vk), Theme.Input);
		keyBtn.Size = new Size(96, 26);
		keyBtn.Margin = new Padding(0, 2, 2, 2);
		keyBtn.AccessibleName = "Key for vJoy button " + b.Button;
		keyBtn.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			SetBindStatus("Press the new key…");
			capture = (int vk) =>
			{
				b.Vk = vk;
				SaveActiveProfile();
				RebuildBindingRows();
				RefreshHotkeyRows();
				SetBindStatus("");
			};
		};
		rowP.Controls.Add(keyBtn, 1, 0);
		rowP.Controls.Add(new Label
		{
			Text = "→",
			ForeColor = Theme.Faint,
			AutoSize = true,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		}, 2, 0);
		Stepper num = new Stepper
		{
			Maximum = vjoyButtonCount,
			Value = Math.Min(Math.Max(b.Button, 1), vjoyButtonCount),
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 2, 4, 2)
		};
		num.ValueChanged += (object? s, EventArgs e) =>
		{
			if (acquired)
			{
				VJoy.SetBtn(Value: false, 1u, (byte)b.Button);
			}
			b.Button = num.Value;
			SaveActiveProfile();
			RefreshHotkeyRows();
		};
		rowP.Controls.Add(num, 3, 0);
		Panel nameBox = new Panel
		{
			BackColor = Theme.Input,
			Height = 26,
			Dock = DockStyle.Fill,
			Padding = new Padding(6, 5, 6, 2),
			Margin = new Padding(0, 2, 2, 2)
		};
		TextBox name = new TextBox
		{
			Text = b.Label ?? "",
			BorderStyle = BorderStyle.None,
			BackColor = Theme.Input,
			ForeColor = Color.White,
			Dock = DockStyle.Fill,
			PlaceholderText = "Add a name…",
			AccessibleName = "Name for vJoy button " + b.Button
		};
		name.TextChanged += (object? s, EventArgs e) =>
		{
			b.Label = name.Text;
			SaveActiveProfile();
			RefreshHotkeyRows();
		};
		nameBox.Controls.Add(name);
		rowP.Controls.Add(nameBox, 4, 0);
		Button del = Styled("×", Theme.Row);
		del.ForeColor = Theme.Muted;
		del.Font = new Font("Segoe UI", 11f);
		del.Size = new Size(26, 26);
		del.Margin = new Padding(2);
		del.AccessibleName = "Remove this button";
		toolTip.SetToolTip(del, "Remove this button");
		del.Click += (object? s, EventArgs e) =>
		{
			if (acquired)
			{
				VJoy.SetBtn(Value: false, 1u, (byte)b.Button);
			}
			profile.Buttons.Remove(b);
			SaveActiveProfile();
			RebuildBindingRows();
			RefreshHotkeyRows();
		};
		rowP.Controls.Add(del, 5, 0);
		Label warn = WarnLabel();
		warn.Margin = new Padding(0, 0, 4, 2);
		rowP.Controls.Add(warn, 0, 1);
		rowP.SetColumnSpan(warn, 6);
		bindRows.Add(new BindRow
		{
			B = b,
			Lamp = lamp,
			Warn = warn
		});
		return rowP;
	}

	private void UpdateBindWarnings()
	{
		foreach (BindRow r in bindRows)
		{
			string w = BindingWarning(r.B);
			r.Warn.Text = w ?? "";
			r.Warn.Visible = w != null;
		}
	}

	// ---------- HUD ----------

	private Control BuildHudPage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		hudPreview = new HudPreview
		{
			Height = 132
		};
		AddRow(t, ref row, hudPreview, SizeType.AutoSize, 0);
		TableLayoutPanel card = Card();
		int cr = 0;
		AddRow(card, ref cr, Header("Style"), SizeType.AutoSize, 0, fill: false, 6);
		segStyle = new Segmented
		{
			Items = new string[4] { "Sidestick", "Minimal", "Strip", "Classic" },
			Text = "HUD style",
			AccessibleName = "HUD style"
		};
		segStyle.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			settings.HudStyle = (HudStyle)segStyle.SelectedIndex;
			overlay?.SetStyle(settings.HudStyle, settings.HudNumbers);
			ConfigurePreview();
			SaveSettingsSoon();
		};
		AddRow(card, ref cr, segStyle, SizeType.AutoSize, 0, fill: true, 6);
		// Size is global and lives in overlay.cfg with the position, like before.
		tbHudSize = AddSliderRow(card, ref cr, "Size", null, OverlayForm.MinSizePercent, OverlayForm.MaxSizePercent, OverlayForm.LoadSizePercent(), "%", autosave: false);
		tbHudSize.DefaultValue = 100;
		toolTip.SetToolTip(tbHudSize, "Double-click to reset to 100%");
		tbHudSize.ValueChanged += (object? s, EventArgs e) =>
		{
			EnsureOverlay();
			overlay.SetSizePercent(tbHudSize.Value);
			ConfigurePreview();
		};
		tbHudOpacity = AddSliderRow(card, ref cr, "Opacity", null, 30, 100, 90, "%", autosave: false);
		tbHudOpacity.ValueChanged += (object? s, EventArgs e) =>
		{
			settings.HudOpacity = tbHudOpacity.Value;
			overlay?.SetOpacityPercent(settings.HudOpacity);
			ConfigurePreview();
			SaveSettingsSoon();
		};
		AddRow(t, ref row, card, SizeType.AutoSize, 0);
		swShowHud = Switch("Show HUD", "while flying", (object? s, EventArgs e) =>
		{
			SetShowHud(swShowHud.Checked);
		});
		swHudNumbers = Switch("Show numbers", "trim and rudder", (object? s, EventArgs e) =>
		{
			settings.HudNumbers = swHudNumbers.Checked;
			overlay?.SetStyle(settings.HudStyle, settings.HudNumbers);
			ConfigurePreview();
			SaveSettingsSoon();
		});
		swHudFade = Switch("Fade when centered", "dims at rest", (object? s, EventArgs e) =>
		{
			settings.HudFade = swHudFade.Checked;
			overlay?.SetFade(settings.HudFade);
			SaveSettingsSoon();
		});
		swHudStandby = Switch("Show in standby", "gray, with the fly key", (object? s, EventArgs e) =>
		{
			settings.HudInStandby = swHudStandby.Checked;
			UpdateOverlayVisibility();
			SaveSettingsSoon();
		});
		TableLayoutPanel pair1 = SwitchPair(swShowHud, swHudNumbers);
		pair1.Margin = new Padding(0, 0, 0, 6);
		AddRow(t, ref row, pair1, SizeType.AutoSize, 0);
		AddRow(t, ref row, SwitchPair(swHudFade, swHudStandby), SizeType.AutoSize, 0);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = false
		};
		btnPositionHud = ActionButton("Position HUD…", Theme.Blue);
		toolTip.SetToolTip(btnPositionHud, "Shows the HUD so you can drag it; click Done on the HUD when it's in place");
		btnPositionHud.Click += (object? s, EventArgs e) =>
		{
			if (hudMoving)
			{
				EndHudPosition();
			}
			else
			{
				BeginHudPosition();
			}
		};
		Button resetPos = ActionButton("Reset position", Theme.Primary);
		toolTip.SetToolTip(resetPos, "Back to the top center of the main screen");
		resetPos.Click += (object? s, EventArgs e) =>
		{
			EnsureOverlay();
			overlay.ResetPosition();
			SetStatus("HUD moved back to the top of the main screen.", Theme.Muted);
		};
		buttons.Controls.Add(btnPositionHud);
		buttons.Controls.Add(resetPos);
		AddRow(t, ref row, buttons, SizeType.AutoSize, 0, fill: true, 10);
		AddRow(t, ref row, Header("Window"), SizeType.AutoSize, 0, fill: false, 6);
		swTopMost = Switch("Always on top", null, (object? s, EventArgs e) =>
		{
			settings.TopMost = swTopMost.Checked;
			TopMost = swTopMost.Checked;
			SaveSettingsSoon();
		});
		swTray = Switch("Close to tray", "keys keep working", (object? s, EventArgs e) =>
		{
			settings.CloseToTray = swTray.Checked;
			tray.Visible = swTray.Checked;
			SaveSettingsSoon();
		});
		swTopMost.Height = 40;
		AddRow(t, ref row, SwitchPair(swTopMost, swTray), SizeType.AutoSize, 0, fill: true, 6);
		TableLayoutPanel compactRow = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		Button toCompact = ActionButton("Compact window", Theme.Primary);
		toCompact.Click += (object? s, EventArgs e) =>
		{
			SetCompact(on: true);
		};
		compactRow.Controls.Add(toCompact, 0, 0);
		compactRow.Controls.Add(new Label
		{
			Text = "Just the essentials, for a second screen.",
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			AutoSize = true,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(4, 0, 0, 0)
		}, 1, 0);
		AddRow(t, ref row, compactRow, SizeType.AutoSize, 0, fill: true, 0);
		return Scroller(t);
	}

	// "Show HUD" appears on the Fly tab and the HUD tab; both switches are the same setting.
	private void SetShowHud(bool on)
	{
		settings.ShowHud = on;
		swShowHud.Checked = on;
		swShowHudFly.Checked = on;
		UpdateOverlayVisibility();
		SaveSettingsSoon();
	}

	private void ConfigurePreview()
	{
		hudPreview.Configure(settings.HudStyle, settings.HudNumbers, settings.HudOpacity, tbHudSize.Value);
	}

	private void ApplySettingsToUi()
	{
		loading = true;
		segStyle.SelectedIndex = (int)settings.HudStyle;
		tbHudOpacity.Value = settings.HudOpacity;
		swShowHud.Checked = settings.ShowHud;
		swShowHudFly.Checked = settings.ShowHud;
		swHudNumbers.Checked = settings.HudNumbers;
		swHudFade.Checked = settings.HudFade;
		swHudStandby.Checked = settings.HudInStandby;
		swTopMost.Checked = settings.TopMost;
		swTray.Checked = settings.CloseToTray;
		TopMost = settings.TopMost;
		tray.Visible = settings.CloseToTray;
		ConfigurePreview();
		loading = false;
		if (settings.Compact)
		{
			SetCompact(on: true);
		}
	}

	private void ApplyProfileToUi()
	{
		loading = true;
		tbRollSens.Value = profile.RollSens;
		tbPitchSens.Value = profile.PitchSens;
		tbDeadzone.Value = profile.Deadzone;
		tbExpo.Value = profile.Expo;
		tbSmooth.Value = profile.Smoothing;
		tbCenterSpeed.Value = profile.CenterSpeed;
		tbTrimRate.Value = profile.TrimRate;
		cbInvertPitch.Checked = profile.InvertPitch;
		cbInvertRoll.Checked = profile.InvertRoll;
		swHideCursor.Checked = profile.HideCursor;
		segMode.SelectedIndex = (profile.SelfCenter ? 1 : 0);
		swRudder.Checked = profile.RudderEnabled;
		tbRudderRate.Value = profile.RudderRatePct;
		RebuildBindingRows();
		ReleaseHeldKeys();
		CancelHotkeyCapture();
		RefreshHotkeyUi();
		UpdateModeUi();
		RefreshProfileUi();
		loading = false;
		lblSaved.Text = "Saved";
		lblSaved.ForeColor = Theme.Faint;
	}

	// ---------- live values (every third tick) ----------

	private void RefreshLiveUi(HudState st)
	{
		if (!Visible || WindowState == FormWindowState.Minimized)
		{
			return;
		}
		if (compact)
		{
			compactMonitor.State = st;
			SetText(lblCompactTrim, "P trim " + HudRenderer.Percent(pitchTrim) + "\nR trim " + HudRenderer.Percent(rollTrim) + "\nRudder " + HudRenderer.Signed(st.Rudder));
			return;
		}
		switch (selectedTab)
		{
		case 0:
		{
			monitor.State = st;
			SetText(lblRoll, HudRenderer.Signed(st.Roll));
			SetText(lblPitch, HudRenderer.Signed(st.Pitch));
			SetText(lblRudder, st.RudderOn ? HudRenderer.Signed(st.Rudder) : "off");
			SetText(lblPitchTrim, HudRenderer.Percent(pitchTrim));
			SetText(lblRollTrim, HudRenderer.Percent(rollTrim));
			int n = Math.Min(vjoyButtonCount, LampStrip.MaxShown);
			bool[] bound = new bool[n];
			bool[] on = new bool[n];
			foreach (ButtonBinding b in profile.Buttons)
			{
				int i = b.Button - 1;
				if (i >= 0 && i < n)
				{
					bound[i] = true;
					on[i] |= b.Pressed;
				}
			}
			lamps.SetLamps(bound, on);
			break;
		}
		case 1:
			curve.SetInput(curveInput, active);
			break;
		case 2:
			foreach (BindRow r in bindRows)
			{
				Color c = (r.B.Pressed ? Theme.Green : Color.FromArgb(70, 70, 80));
				if (r.Lamp.BackColor != c)
				{
					r.Lamp.BackColor = c;
				}
			}
			break;
		case 3:
		{
			string note = ((!settings.ShowHud) ? "The HUD is off: turn on Show HUD" : ((!active && !settings.HudInStandby && !hudMoving) ? "Hidden in standby: turn on Show in standby" : ""));
			bool centered = Math.Abs(st.Roll - st.RollTrim) < 0.06 && Math.Abs(st.Pitch - st.PitchTrim) < 0.06;
			hudPreview.ShowFrame(st, (settings.HudFade && active && centered) ? 0.35 : 1.0, note);
			break;
		}
		}
	}

	// ---------- compact window and tray ----------

	private Control BuildCompact()
	{
		TableLayoutPanel t = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			BackColor = Theme.Bg,
			Padding = new Padding(12, 10, 12, 10),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 120f),
				new ColumnStyle(SizeType.Absolute, 80f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		TableLayoutPanel state = new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = Padding.Empty
		};
		lblCompactState = new Label
		{
			AutoSize = true,
			Font = Theme.Header,
			Margin = new Padding(0, 0, 0, 2)
		};
		lblCompactProfile = new Label
		{
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(116, 20),
			Font = Theme.Strong,
			ForeColor = Color.White,
			Margin = Padding.Empty
		};
		state.Controls.Add(lblCompactState, 0, 0);
		state.Controls.Add(lblCompactProfile, 0, 1);
		t.Controls.Add(state, 0, 0);
		compactMonitor = new StickMonitor
		{
			Size = new Size(64, 67),
			BackColor = Theme.Bg,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		};
		t.Controls.Add(compactMonitor, 1, 0);
		lblCompactTrim = new Label
		{
			AutoSize = true,
			ForeColor = Theme.Amber,
			Font = Theme.Small,
			Anchor = AnchorStyles.Left,
			Margin = Padding.Empty
		};
		t.Controls.Add(lblCompactTrim, 2, 0);
		Button expand = ActionButton("Expand", Theme.Primary);
		expand.Anchor = AnchorStyles.Right;
		expand.Margin = Padding.Empty;
		toolTip.SetToolTip(expand, "Back to the full window");
		expand.Click += (object? s, EventArgs e) =>
		{
			SetCompact(on: false);
		};
		t.Controls.Add(expand, 3, 0);
		return t;
	}

	private void SetCompact(bool on)
	{
		compact = on;
		settings.Compact = on;
		SaveSettingsSoon();
		SuspendLayout();
		root.SuspendLayout();
		for (int i = 0; i < HeaderRows.Length; i++)
		{
			root.RowStyles[i].Height = (on ? 0 : HeaderRows[i]);
			headerParts[i].Visible = !on;
		}
		pageHost.Visible = !on;
		root.RowStyles[4].Height = (on ? CompactBodyHeight : 0);
		compactPanel.Visible = on;
		ClientSize = (on ? new Size(CompactWidth, CompactBodyHeight + StatusHeight) : new Size(FullWidth, FullHeight));
		root.ResumeLayout();
		ResumeLayout();
		RefreshCompact();
	}

	private void RefreshCompact()
	{
		lblCompactState.Text = (active ? "● FLYING" : "● STANDBY");
		lblCompactState.ForeColor = (active ? Color.LightGreen : Theme.Muted);
	}

	private void BuildTray()
	{
		tray = new NotifyIcon
		{
			Text = "MouseYokeEnhanced",
			Icon = BrandIcon.Tray(active: false),
			Visible = false
		};
		ContextMenuStrip m = DarkMenuRenderer.NewMenu();
		ToolStripMenuItem show = new ToolStripMenuItem("Show MouseYoke", null, (object? s, EventArgs e) =>
		{
			ShowFromTray();
		})
		{
			Font = Theme.Strong
		};
		ToolStripMenuItem fly = new ToolStripMenuItem("Activate", null, (object? s, EventArgs e) =>
		{
			ToggleActive();
		});
		m.Items.Add(show);
		m.Items.Add(fly);
		m.Items.Add(new ToolStripSeparator());
		m.Items.Add("Exit", null, (object? s, EventArgs e) =>
		{
			ExitApp();
		});
		m.Opening += (object? s, System.ComponentModel.CancelEventArgs e) =>
		{
			fly.Text = (active ? "Stop flying" : "Activate");
			fly.Enabled = acquired;
		};
		tray.ContextMenuStrip = m;
		tray.DoubleClick += (object? s, EventArgs e) =>
		{
			ShowFromTray();
		};
	}
}
