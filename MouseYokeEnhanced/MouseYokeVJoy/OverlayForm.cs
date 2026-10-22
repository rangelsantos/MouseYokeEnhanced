using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseYokeVJoy;

public class OverlayForm : Form
{
	private double roll;

	private double pitch;

	private double pitchTrim;

	private double rollTrim;

	private bool active;

	private bool movable;

	private bool dragging;

	private Point dragStart;

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 32;

	private static string PosPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlay.cfg");

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 524448;
			return createParams;
		}
	}

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	public OverlayForm()
	{
		AutoScaleMode = AutoScaleMode.Font;
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		TopMost = true;
		StartPosition = FormStartPosition.Manual;
		Size = new Size(260, 260);
		BackColor = Color.Magenta;
		TransparencyKey = Color.Magenta;
		Opacity = 0.92;
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Location = LoadPosition() ?? DefaultLocation();
	}

	private Point DefaultLocation()
	{
		Rectangle scr = Screen.PrimaryScreen.Bounds;
		return new Point(scr.Left + (scr.Width - Width) / 2, scr.Top + 40);
	}

	private Point? LoadPosition()
	{
		try
		{
			if (File.Exists(PosPath))
			{
				string[] p = File.ReadAllText(PosPath).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length >= 2 && int.TryParse(p[0], out var x) && int.TryParse(p[1], out var y))
				{
					Point pt = new Point(x, y);
					Screen[] allScreens = Screen.AllScreens;
					for (int i = 0; i < allScreens.Length; i++)
					{
						if (allScreens[i].Bounds.IntersectsWith(new Rectangle(pt, Size)))
						{
							return pt;
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void SavePosition()
	{
		try
		{
			File.WriteAllText(PosPath, $"{Location.X} {Location.Y}");
		}
		catch
		{
		}
	}

	public void SetMovable(bool on)
	{
		movable = on;
		if (IsHandleCreated)
		{
			int ex = GetWindowLong(Handle, -20);
			ex = ((!on) ? (ex | 0x20) : (ex & -33));
			SetWindowLong(Handle, -20, ex);
		}
		Opacity = (on ? 1.0 : 0.92);
		if (!on)
		{
			SavePosition();
		}
		Invalidate();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (movable && e.Button == MouseButtons.Left)
		{
			dragging = true;
			dragStart = e.Location;
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (dragging)
		{
			dragging = false;
			SavePosition();
		}
		base.OnMouseUp(e);
	}

	public void UpdateValues(double roll, double pitch, double pitchTrim, double rollTrim, bool active)
	{
		this.roll = roll;
		this.pitch = pitch;
		this.pitchTrim = pitchTrim;
		this.rollTrim = rollTrim;
		this.active = active;
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(TransparencyKey);
		int pad = 12;
		Rectangle panel = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
		float cx = (float)panel.Left + (float)panel.Width / 2f;
		float cy = (float)panel.Top + (float)panel.Height / 2f;
		float r = (float)Math.Min(panel.Width, panel.Height) / 2f - 6f;
		using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 12, 14, 20)))
		{
			using GraphicsPath path = Rounded(panel, 18);
			g.FillPath(bg, path);
		}
		GraphicsPath clip = new GraphicsPath();
		clip.AddEllipse(cx - r, cy - r, r * 2f, r * 2f);
		GraphicsState clipState = g.Save();
		g.SetClip(clip);
		GraphicsState state = g.Save();
		g.TranslateTransform(cx, cy + (float)(pitch * (double)r * 0.65));
		g.RotateTransform((float)((0.0 - roll) * 45.0));
		using (SolidBrush sky = new SolidBrush(Color.FromArgb(70, 60, 130, 200)))
		{
			g.FillRectangle(sky, (0f - r) * 1.6f, (0f - r) * 1.6f, r * 3.2f, r * 1.6f);
		}
		using (SolidBrush ground = new SolidBrush(Color.FromArgb(70, 120, 80, 40)))
		{
			g.FillRectangle(ground, (0f - r) * 1.6f, 0f, r * 3.2f, r * 1.6f);
		}
		using (Pen hz = new Pen(Color.FromArgb(220, 0, 220, 255), 1.6f))
		{
			g.DrawLine(hz, (0f - r) * 1.6f, 0f, r * 1.6f, 0f);
		}
		g.Restore(state);
		g.Restore(clipState);
		clip.Dispose();
		using (Pen ring = new Pen(Color.FromArgb(120, 90, 150, 170), 1.4f))
		{
			g.DrawEllipse(ring, cx - r, cy - r, r * 2f, r * 2f);
		}
		using (Pen dz = new Pen(Color.FromArgb(70, 200, 200, 200), 1f)
		{
			DashStyle = DashStyle.Dot
		})
		{
			g.DrawEllipse(dz, cx - r * 0.08f, cy - r * 0.08f, r * 0.16f, r * 0.16f);
		}
		using (Pen acp = new Pen(Color.FromArgb(255, 255, 255, 255), 2.2f))
		{
			g.DrawLine(acp, cx - 26f, cy, cx - 8f, cy);
			g.DrawLine(acp, cx + 8f, cy, cx + 26f, cy);
			g.DrawLine(acp, cx - 8f, cy, cx, cy + 7f);
			g.DrawLine(acp, cx + 8f, cy, cx, cy + 7f);
		}
		float ty = cy + (float)(pitchTrim * (double)r);
		using (Pen tp = new Pen(Color.FromArgb(255, 255, 190, 60), 2f))
		{
			g.DrawLine(tp, cx + r - 14f, ty, cx + r - 2f, ty);
		}
		float dx = cx + (float)(roll * (double)r);
		float dy = cy + (float)(pitch * (double)r);
		using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, 120, 255, 140)))
		{
			g.FillEllipse(glow, dx - 11f, dy - 11f, 22f, 22f);
		}
		using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 120, 255, 140)))
		{
			g.FillEllipse(dot, dx - 5f, dy - 5f, 10f, 10f);
		}
		using (Font f = new Font("Segoe UI", 8f, FontStyle.Bold))
		{
			string bank;
			if (Math.Abs(roll) < 0.03)
			{
				bank = "WINGS LVL";
			}
			else
			{
				bank = ((roll > 0.0) ? "BANK R" : "BANK L");
			}
			using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 0, 220, 255)))
			{
				g.DrawString(bank, f, b, panel.Left + 8, panel.Bottom - 20);
			}
			using (SolidBrush b2 = new SolidBrush(active ? Color.FromArgb(230, 120, 255, 140) : Color.FromArgb(230, 200, 120, 120)))
			{
				string s = (active ? "ACTIVE" : "STANDBY");
				SizeF sz = g.MeasureString(s, f);
				g.DrawString(s, f, b2, (float)panel.Right - sz.Width - 8f, panel.Top + 4);
			}
			using SolidBrush b3 = new SolidBrush(Color.FromArgb(230, 255, 190, 60));
			g.DrawString($"TRIM {pitchTrim * 100.0:+0;-0;0}", f, b3, panel.Left + 8, panel.Top + 4);
		}
		if (!movable)
		{
			return;
		}
		using (Pen mv = new Pen(Color.FromArgb(255, 90, 200, 255), 2.5f)
		{
			DashStyle = DashStyle.Dash
		})
		{
			using GraphicsPath mpath = Rounded(new Rectangle(panel.Left, panel.Top, panel.Width, panel.Height), 18);
			g.DrawPath(mv, mpath);
		}
		using Font f2 = new Font("Segoe UI", 8f, FontStyle.Bold);
		using SolidBrush b4 = new SolidBrush(Color.FromArgb(240, 90, 200, 255));
		string s2 = "drag to move";
		g.DrawString(s2, f2, b4, cx - g.MeasureString(s2, f2).Width / 2f, panel.Bottom - 20);
	}

	private static GraphicsPath Rounded(Rectangle r, int rad)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, rad, rad, 180f, 90f);
		graphicsPath.AddArc(r.Right - rad, r.Y, rad, rad, 270f, 90f);
		graphicsPath.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - rad, rad, rad, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
