using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;

namespace MouseYokeVJoy;

public enum HudStyle
{
	Sidestick,
	Minimal,
	Strip,
	Classic
}

// How the HUD looks: the app-wide options that change the drawing but not the live values. Background is the
// panel behind it in percent; Status draws the active/standby line.
public readonly record struct HudLook(HudStyle Style, bool Numbers, int Background, bool Status)
{
	// Without much panel, text and marks get a dark outline so they read over a bright sky. Minimal has no panel
	// and already draws its own shadow.
	public bool Outlined => Background < 50 && Style != HudStyle.Minimal;
}

// What the HUD and the Fly monitor show: the stick output sent to vJoy (trim included), plus context.
public struct HudState
{
	public double Roll;

	public double Pitch;

	public double Rudder;

	public double PitchTrim;

	public double RollTrim;

	public double Deadzone;

	public bool Active;

	// Flying, but the freelook key is held: the stick holds and the mouse is free.
	public bool Freelook;

	public bool Spring;

	public bool RudderOn;

	public string ToggleKey;

	// Which optional elements are drawn; the layout closes up around the hidden ones.
	public bool ShowRudder;

	public bool ShowPitchTrim;

	public bool ShowRollTrim;
}

// Draws every HUD style in its own logical units; callers scale the Graphics to the size they want.
internal static class HudRenderer
{
	private static readonly string FontName = (FontFamily.Families.Any((FontFamily f) => f.Name == "Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : "Segoe UI Semibold");

	private static readonly Dictionary<float, Font> Fonts = new Dictionary<float, Font>();

	private static readonly StringFormat Typographic = StringFormat.GenericTypographic;

	public const int MonitorWidth = 240;

	public const int MonitorHeight = 250;

	// The bands a hidden status line gives back: the top row of the sidestick, the left end of the strip.
	private const int SidestickStatusBand = 24;

	private const float StripStatusBand = 70f;

	private static readonly Color OutlineColor = Color.FromArgb(200, 0, 0, 0);

	// The Fly monitor: no panel, no text.
	private static readonly HudLook MonitorLook = new HudLook(HudStyle.Sidestick, Numbers: false, Background: 100, Status: true);

	// Depends on which optional elements are shown: a hidden one leaves no empty space.
	public static Size LogicalSize(HudLook look, HudState s)
	{
		return look.Style switch
		{
			HudStyle.Minimal => new Size(160, (int)MinimalHeight(s, out _)),
			HudStyle.Strip => new Size((int)StripWidth(look, s, out _, out _), 58),
			HudStyle.Classic => new Size(260, (int)ClassicPanelHeight(s) + 24),
			_ => new Size(SidestickWidth(s), (int)SidestickRows(s, look.Numbers, out _, out _, out _) - SidestickLift(look)),
		};
	}

	private static int SidestickLift(HudLook look)
	{
		return look.Status ? 0 : SidestickStatusBand;
	}

	private static float StripLift(HudLook look)
	{
		return look.Status ? 0f : StripStatusBand;
	}

	// A panel's alpha scaled by the Background setting.
	private static int PanelAlpha(HudLook look, int full)
	{
		return full * look.Background / 100;
	}

	// Without the pitch trim scale the box is centered (28 px on each side).
	private static int SidestickWidth(HudState s)
	{
		return s.ShowPitchTrim ? 240 : 216;
	}

	private static bool SidestickNumbers(HudState s, bool numbers)
	{
		return numbers && (s.ShowRudder || s.ShowPitchTrim || s.ShowRollTrim);
	}

	// The rows under the stick box, packed in order: roll trim scale, rudder track, numbers. Returns the height.
	private static float SidestickRows(HudState s, bool numbers, out float rollY, out float rudderY, out float numbersY)
	{
		float y = 200f;
		rollY = y + 10f;
		if (s.ShowRollTrim)
		{
			y += 18f;
		}
		rudderY = y + 10f;
		if (s.ShowRudder)
		{
			y += 23f;
		}
		numbersY = y + 23f;
		return y + (SidestickNumbers(s, numbers) ? 39f : 19f);
	}

	private static float MinimalHeight(HudState s, out float rudderY)
	{
		rudderY = (s.ShowRollTrim ? 152f : 146f);
		return s.ShowRudder ? (rudderY + 8f) : (s.ShowRollTrim ? 148f : 142f);
	}

	private static float StripWidth(HudLook look, HudState s, out float rudderX, out float trimX)
	{
		float x = (s.ShowPitchTrim ? 140f : 134f) - StripLift(look);
		rudderX = x + 10f;
		if (s.ShowRudder)
		{
			x = rudderX + 100f;
		}
		trimX = x + 16f;
		if (s.ShowPitchTrim || s.ShowRollTrim)
		{
			return trimX + 94f;
		}
		return x + (s.ShowRudder ? 16f : 10f);
	}

	// Without the rudder ball under the ring the panel ends just below the bottom labels.
	private static float ClassicPanelHeight(HudState s)
	{
		return s.ShowRudder ? 260f : 246f;
	}

	// "P −14%   R +8%", only the trims that are shown.
	private static string TrimText(HudState s, string gap)
	{
		string p = (s.ShowPitchTrim ? ("P " + Percent(s.PitchTrim)) : "");
		string r = (s.ShowRollTrim ? ("R " + Percent(s.RollTrim)) : "");
		return p + ((p.Length > 0 && r.Length > 0) ? gap : "") + r;
	}

	public static void Draw(Graphics g, HudLook look, HudState s)
	{
		switch (look.Style)
		{
		case HudStyle.Minimal:
			DrawMinimal(g, look, s);
			break;
		case HudStyle.Strip:
			DrawStrip(g, look, s);
			break;
		case HudStyle.Classic:
			DrawClassic(g, look, s);
			break;
		default:
			DrawSidestick(g, look, s, panel: true, labels: true);
			break;
		}
	}

	// The Fly tab monitor: the sidestick drawing without panel or text, 240 x 250. It always shows every
	// element, so the user sees what can be set up even when the HUD hides it.
	public static void DrawMonitor(Graphics g, HudState s)
	{
		s.ShowRudder = (s.ShowPitchTrim = (s.ShowRollTrim = true));
		DrawSidestick(g, MonitorLook, s, panel: false, labels: false);
	}

	// With the status line hidden, standby and freelook show as a fixed notice in the middle (a real notice,
	// like "TRIM RESET", takes its place while it lasts); flying shows nothing.
	public static string StatusNotice(HudLook look, HudState s)
	{
		if (look.Status || s.Active)
		{
			return null;
		}
		if (s.Freelook)
		{
			return "FREELOOK";
		}
		return string.IsNullOrEmpty(s.ToggleKey) ? "STANDBY" : ("STANDBY · " + s.ToggleKey);
	}

	// A short notice in the middle of the HUD ("ACTIVE", "TRIM RESET"), drawn over everything else.
	public static void DrawToast(Graphics g, string text, Size area)
	{
		Font f = F(14f);
		SizeF ts = g.MeasureString(text, f, PointF.Empty, Typographic);
		float w = ts.Width + 24f;
		float h = 28f;
		float x = ((float)area.Width - w) / 2f;
		float y = ((float)area.Height - h) / 2f;
		FillRounded(g, Color.FromArgb(225, 10, 13, 19), x, y, w, h, 6f);
		using (Pen edge = new Pen(A(Theme.Cyan, 0.6), 1f))
		{
			using GraphicsPath p = Rounded(new RectangleF(x, y, w, h), 6f);
			g.DrawPath(edge, p);
		}
		Text(g, text, 14f, Color.White, (float)area.Width / 2f, y + 19f, StringAlignment.Center);
	}

	public static string Percent(double v)
	{
		int n = (int)Math.Round(Math.Abs(v) * 100.0);
		if (n == 0)
		{
			return "0%";
		}
		return ((v < 0.0) ? "−" : "+") + n + "%";
	}

	public static string Signed(double v)
	{
		string r = Math.Abs(v).ToString("0.00", CultureInfo.InvariantCulture);
		return ((v < 0.0 && r != "0.00") ? "−" : "+") + r;
	}

	private static Font F(float px)
	{
		if (!Fonts.TryGetValue(px, out var f))
		{
			f = new Font(FontName, px, FontStyle.Regular, GraphicsUnit.Pixel);
			Fonts[px] = f;
		}
		return f;
	}

	private static Color A(Color c, double alpha)
	{
		return Color.FromArgb((int)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0), c.R, c.G, c.B);
	}

	private static Color FrameColor(bool active)
	{
		return active ? A(Theme.Cyan, 0.95) : Color.FromArgb(191, 160, 168, 178);
	}

	private static Color MarkColor(bool active)
	{
		return active ? Theme.Green : Color.FromArgb(230, 190, 195, 200);
	}

	private static Color TextColor(bool active)
	{
		return active ? Theme.Green : Color.FromArgb(230, 200, 205, 210);
	}

	// Text placed on its baseline, like a canvas fillText. Outlined, it is drawn as a path with a dark stroke.
	private static void Text(Graphics g, string text, float px, Color color, float x, float baseline, StringAlignment align = StringAlignment.Near, bool outlined = false)
	{
		Font f = F(px);
		float ascent = px * (float)f.FontFamily.GetCellAscent(f.Style) / (float)f.FontFamily.GetEmHeight(f.Style);
		float left = x;
		if (align != StringAlignment.Near)
		{
			float w = g.MeasureString(text, f, PointF.Empty, Typographic).Width;
			left = ((align == StringAlignment.Far) ? (x - w) : (x - w / 2f));
		}
		if (!outlined)
		{
			using SolidBrush b = new SolidBrush(color);
			g.DrawString(text, f, b, left, baseline - ascent, Typographic);
			return;
		}
		using GraphicsPath path = new GraphicsPath();
		path.AddString(text, f.FontFamily, (int)f.Style, px, new PointF(left, baseline - ascent), Typographic);
		using (Pen stroke = new Pen(Color.FromArgb(215, 0, 0, 0), Math.Max(2.2f, px * 0.2f))
		{
			LineJoin = LineJoin.Round
		})
		{
			g.DrawPath(stroke, path);
		}
		using SolidBrush fill = new SolidBrush(color);
		g.FillPath(fill, path);
	}

	// Outlined: a dark, wider copy of a line under it, so a thin mark reads over a bright sky.
	private static void Under(Graphics g, bool outlined, float width, float x1, float y1, float x2, float y2)
	{
		if (!outlined)
		{
			return;
		}
		using Pen pen = new Pen(OutlineColor, width + 2.5f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		g.DrawLine(pen, x1, y1, x2, y2);
	}

	public static GraphicsPath Rounded(RectangleF r, float rad)
	{
		float d = rad * 2f;
		GraphicsPath p = new GraphicsPath();
		p.AddArc(r.X, r.Y, d, d, 180f, 90f);
		p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		p.CloseFigure();
		return p;
	}

	private static void FillRounded(Graphics g, Color c, float x, float y, float w, float h, float rad)
	{
		using GraphicsPath p = Rounded(new RectangleF(x, y, w, h), rad);
		using SolidBrush b = new SolidBrush(c);
		g.FillPath(b, p);
	}

	private static void Brackets(Graphics g, float x, float y, float s, float len, Color color, float width, bool outlined = false)
	{
		if (outlined)
		{
			Brackets(g, x, y, s, len, OutlineColor, width + 2.5f);
		}
		using Pen pen = new Pen(color, width)
		{
			StartCap = LineCap.Square,
			EndCap = LineCap.Square,
			LineJoin = LineJoin.Miter
		};
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(x, y + len),
			new PointF(x, y),
			new PointF(x + len, y)
		});
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(x + s - len, y),
			new PointF(x + s, y),
			new PointF(x + s, y + len)
		});
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(x + s, y + s - len),
			new PointF(x + s, y + s),
			new PointF(x + s - len, y + s)
		});
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(x + len, y + s),
			new PointF(x, y + s),
			new PointF(x, y + s - len)
		});
	}

	// The stick marker: a cross with a dark outline so it reads over a bright sky.
	private static void Cross(Graphics g, float x, float y, float arm, Color color, float width)
	{
		using (Pen outline = new Pen(Color.FromArgb(140, 0, 0, 0), width + 2.5f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		})
		{
			g.DrawLine(outline, x - arm, y, x + arm, y);
			g.DrawLine(outline, x, y - arm, x, y + arm);
		}
		using Pen pen = new Pen(color, width)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		g.DrawLine(pen, x - arm, y, x + arm, y);
		g.DrawLine(pen, x, y - arm, x, y + arm);
	}

	// A trim pointer; "left" points at x from the right, otherwise it points up at y from below.
	private static void Pointer(Graphics g, float x, float y, bool left, float s, Color color, bool outlined = false)
	{
		PointF[] pts = (left ? new PointF[3]
		{
			new PointF(x, y),
			new PointF(x + s, y - s * 0.7f),
			new PointF(x + s, y + s * 0.7f)
		} : new PointF[3]
		{
			new PointF(x, y),
			new PointF(x - s * 0.7f, y + s),
			new PointF(x + s * 0.7f, y + s)
		});
		if (outlined)
		{
			using Pen edge = new Pen(OutlineColor, 2.5f)
			{
				LineJoin = LineJoin.Round
			};
			g.DrawPolygon(edge, pts);
		}
		using SolidBrush b = new SolidBrush(color);
		g.FillPolygon(b, pts);
	}

	// Outlined, a solid dot gets a dark ring; a faint one (a glow, an unused ball) stays as it is.
	private static void Dot(Graphics g, Color c, float x, float y, float r, bool outlined = false)
	{
		if (outlined && c.A > 150)
		{
			using SolidBrush ring = new SolidBrush(OutlineColor);
			g.FillEllipse(ring, x - r - 1.7f, y - r - 1.7f, (r + 1.7f) * 2f, (r + 1.7f) * 2f);
		}
		using SolidBrush b = new SolidBrush(c);
		g.FillEllipse(b, x - r, y - r, r * 2f, r * 2f);
	}

	// 240 x 280 with everything shown (260 without numbers). Inspired by the Airbus sidestick indicator: the
	// brackets are full travel, the cross is the output, the amber pointers are the trims and the bottom bar is the rudder.
	// Without the status row everything moves up into its band.
	private static void DrawSidestick(Graphics g, HudLook look, HudState s, bool panel, bool labels)
	{
		bool numbers = look.Numbers;
		bool o = look.Outlined;
		float width = SidestickWidth(s);
		float height = SidestickRows(s, numbers, out var rollY, out var rudderY, out var numbersY);
		int lift = (labels ? SidestickLift(look) : 0);
		if (panel && look.Background > 0)
		{
			using GraphicsPath p = Rounded(new RectangleF(4f, 4f, width - 8f, height - 8f - (float)lift), 14f);
			using SolidBrush bg = new SolidBrush(Color.FromArgb(PanelAlpha(look, 160), 10, 13, 19));
			using Pen edge = new Pen(Color.FromArgb(PanelAlpha(look, 18), 255, 255, 255), 1f);
			g.FillPath(bg, p);
			g.DrawPath(edge, p);
		}
		GraphicsState lifted = g.Save();
		g.TranslateTransform(0f, -lift);
		labels = labels && look.Status;
		float x0 = 28f;
		float y0 = 40f;
		float size = 160f;
		float cx = 108f;
		float cy = 120f;
		float h = 80f;
		if (labels)
		{
			Color tc = TextColor(s.Active);
			Dot(g, tc, 22f, 22f, 3.5f, o);
			string standby = (string.IsNullOrEmpty(s.ToggleKey) ? "STANDBY" : ("STANDBY · " + s.ToggleKey));
			Text(g, s.Active ? "ACTIVE" : (s.Freelook ? "FREELOOK" : standby), 12f, tc, 32f, 26f, outlined: o);
			Text(g, s.Spring ? "SPRING" : "DIRECT", 12f, A(Theme.Cyan, 0.85), width - 28f, 26f, StringAlignment.Far, o);
		}
		if (o)
		{
			// The faint center guide vanishes without a panel: each dash gets a thin dark edge and more white. Flat
			// caps and a wider gap keep the dashes apart, so it stays a light guide under the cross.
			using Pen shade = new Pen(Color.FromArgb(104, 0, 0, 0), 1.5f);
			using Pen dash = new Pen(Color.FromArgb(165, 255, 255, 255), 1f);
			for (float d = 8f; d < size - 8f; d += 9f)
			{
				float e = Math.Min(d + 4f, size - 8f);
				g.DrawLine(shade, x0 + d, cy, x0 + e, cy);
				g.DrawLine(shade, cx, y0 + d, cx, y0 + e);
			}
			for (float d = 8f; d < size - 8f; d += 9f)
			{
				float e = Math.Min(d + 4f, size - 8f);
				g.DrawLine(dash, x0 + d, cy, x0 + e, cy);
				g.DrawLine(dash, cx, y0 + d, cx, y0 + e);
			}
		}
		else
		{
			using Pen faint = new Pen(Color.FromArgb(41, 255, 255, 255), 1f)
			{
				DashPattern = new float[2] { 3f, 4f }
			};
			g.DrawLine(faint, x0 + 8f, cy, x0 + size - 8f, cy);
			g.DrawLine(faint, cx, y0 + 8f, cx, y0 + size - 8f);
		}
		if (!s.Spring && s.Deadzone > 0.0)
		{
			float r = Math.Max(3f, (float)s.Deadzone * h);
			if (o)
			{
				using Pen ring = new Pen(Color.FromArgb(136, 0, 0, 0), 1.6f);
				g.DrawEllipse(ring, cx - r, cy - r, r * 2f, r * 2f);
			}
			using Pen dz = new Pen(Color.FromArgb(o ? 210 : 72, 255, 255, 255), o ? 1.2f : 1f)
			{
				DashPattern = new float[2] { 1.5f, 3f }
			};
			g.DrawEllipse(dz, cx - r, cy - r, r * 2f, r * 2f);
		}
		Brackets(g, x0, y0, size, 22f, FrameColor(s.Active), 2.2f, o);
		using (Pen scale = new Pen(Color.FromArgb(77, 255, 255, 255), 1.2f))
		{
			if (s.ShowPitchTrim)
			{
				Under(g, o, 1.2f, 200f, y0, 200f, y0 + size);
				g.DrawLine(scale, 200f, y0, 200f, y0 + size);
				for (int i = 0; i <= 4; i++)
				{
					float ty = y0 + (float)(i * 40);
					g.DrawLine(scale, 200f, ty, (i == 2) ? 208f : 204f, ty);
				}
			}
			if (s.ShowRollTrim)
			{
				Under(g, o, 1.2f, x0, rollY, x0 + size, rollY);
				g.DrawLine(scale, x0, rollY, x0 + size, rollY);
				for (int j = 0; j <= 4; j++)
				{
					float tx = x0 + (float)(j * 40);
					g.DrawLine(scale, tx, rollY, tx, rollY - ((j == 2) ? 8f : 4f));
				}
			}
		}
		if (s.ShowPitchTrim)
		{
			Pointer(g, 203f, cy + (float)s.PitchTrim * h, left: true, 8f, Theme.Amber, o);
		}
		if (s.ShowRollTrim)
		{
			Pointer(g, cx + (float)s.RollTrim * h, rollY + 3f, left: false, 8f, Theme.Amber, o);
		}
		if (s.ShowRudder)
		{
			FillRounded(g, o ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(26, 255, 255, 255), x0, rudderY, size, 10f, 5f);
			float rx = cx + (float)s.Rudder * h;
			using (SolidBrush rb = new SolidBrush(s.Active ? A(Theme.Cyan, 0.75) : Color.FromArgb(128, 160, 168, 178)))
			{
				g.FillRectangle(rb, Math.Min(cx, rx), rudderY + 1f, Math.Abs(rx - cx), 8f);
			}
			using (SolidBrush tick = new SolidBrush(Color.White))
			{
				g.FillRectangle(tick, cx - 0.75f, rudderY - 3f, 1.5f, 16f);
			}
			if (s.ShowPitchTrim)
			{
				Text(g, "RUD", 10f, Color.FromArgb(179, 220, 225, 230), 196f, rudderY + 9f, outlined: o);
			}
			else
			{
				// No room right of the track in the narrow panel: the label goes inside its left end.
				Text(g, "RUD", 9f, Color.FromArgb(204, 220, 225, 230), x0 + 6f, rudderY + 8.5f, outlined: o);
			}
			if (!s.RudderOn)
			{
				// Inside the right half of the empty track, clear of the roll trim pointer above the center.
				Text(g, "OFF", 9f, Color.FromArgb(204, 220, 225, 230), cx + 44f, rudderY + 8.5f, StringAlignment.Center, o);
			}
		}
		Cross(g, cx + (float)s.Roll * h, cy + (float)s.Pitch * h, 9f, MarkColor(s.Active), 2.6f);
		if (SidestickNumbers(s, numbers))
		{
			string trim = TrimText(s, "   ");
			if (trim.Length > 0)
			{
				Text(g, "TRIM  " + trim, 12f, Theme.Amber, 18f, numbersY, outlined: o);
			}
			if (s.ShowRudder)
			{
				Text(g, "RUD " + Signed(s.Rudder), 12f, A(Theme.Cyan, 0.9), width - 18f, numbersY, StringAlignment.Far, o);
			}
		}
		g.Restore(lifted);
	}

	// 160 x 160 with everything shown, no panel. Every mark is drawn twice, first as a dark offset copy, so it reads on a bright sky.
	private static void DrawMinimal(Graphics g, HudLook look, HudState s)
	{
		DrawMinimalLayer(g, look, s, shadow: true);
		DrawMinimalLayer(g, look, s, shadow: false);
	}

	private static void DrawMinimalLayer(Graphics g, HudLook look, HudState s, bool shadow)
	{
		Color sh = Color.FromArgb(150, 0, 0, 0);
		float o = (shadow ? 1.2f : 0f);
		float x0 = 28f;
		float size = 104f;
		float cx = 80f;
		float cy = 80f;
		float h = 52f;
		Brackets(g, x0 + o, x0 + o, size, 16f, shadow ? sh : FrameColor(s.Active), 2.2f);
		if (look.Status)
		{
			Dot(g, shadow ? sh : TextColor(s.Active), x0 + o, 16f + o, 3.5f);
		}
		if (!s.Active && look.Status)
		{
			Text(g, s.Freelook ? "LOOK" : "STBY", 10f, shadow ? sh : TextColor(active: false), x0 + 8f + o, 20f + o);
		}
		if (s.ShowPitchTrim)
		{
			Pointer(g, 137f + o, cy + (float)s.PitchTrim * h + o, left: true, 7f, shadow ? sh : Theme.Amber);
		}
		if (s.ShowRollTrim)
		{
			Pointer(g, cx + (float)s.RollTrim * h + o, 137f + o, left: false, 7f, shadow ? sh : Theme.Amber);
		}
		if (s.ShowRudder)
		{
			MinimalHeight(s, out var ry);
			using (Pen line = new Pen(shadow ? sh : Color.FromArgb(128, 255, 255, 255), 1.2f))
			{
				g.DrawLine(line, x0 + o, ry + o, x0 + size + o, ry + o);
				g.DrawLine(line, cx + o, ry - 4f + o, cx + o, ry + 4f + o);
			}
			Dot(g, shadow ? sh : FrameColor(s.Active), cx + (float)s.Rudder * h + o, ry + o, 3f);
		}
		if (shadow)
		{
			using Pen pen = new Pen(sh, 4.4f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			float mx = cx + (float)s.Roll * h + o;
			float my = cy + (float)s.Pitch * h + o;
			g.DrawLine(pen, mx - 8f, my, mx + 8f, my);
			g.DrawLine(pen, mx, my - 8f, mx, my + 8f);
		}
		else
		{
			Cross(g, cx + (float)s.Roll * h, cy + (float)s.Pitch * h, 8f, MarkColor(s.Active), 2.4f);
		}
	}

	// 360 x 58 with everything shown: a thin bar for a screen edge. Hidden elements (and the status) make it shorter.
	private static void DrawStrip(Graphics g, HudLook look, HudState s)
	{
		bool o = look.Outlined;
		float width = StripWidth(look, s, out var rudX, out var trimX);
		if (look.Background > 0)
		{
			FillRounded(g, Color.FromArgb(PanelAlpha(look, 168), 10, 13, 19), 2f, 2f, width - 4f, 54f, 10f);
		}
		Color tc = TextColor(s.Active);
		if (look.Status)
		{
			Dot(g, tc, 16f, 29f, 3.5f, o);
			Text(g, s.Active ? "ACTIVE" : (s.Freelook ? "LOOK" : "STBY"), 12f, tc, 25f, 33f, outlined: o);
		}
		float bx = 86f - StripLift(look);
		float by = 8f;
		float bc = bx + 21f;
		float bcy = by + 21f;
		Brackets(g, bx, by, 42f, 8f, FrameColor(s.Active), 1.8f, o);
		if (s.ShowPitchTrim)
		{
			Pointer(g, bx + 45f, bcy + (float)s.PitchTrim * 21f, left: true, 5f, Theme.Amber, o);
		}
		Cross(g, bc + (float)s.Roll * 21f, bcy + (float)s.Pitch * 21f, 5f, MarkColor(s.Active), 2f);
		if (s.ShowRudder)
		{
			float mid = rudX + 50f;
			Text(g, s.RudderOn ? "RUD" : "RUD OFF", 10f, Color.FromArgb(179, 220, 225, 230), rudX, 24f, outlined: o);
			FillRounded(g, o ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(31, 255, 255, 255), rudX, 30f, 100f, 8f, 4f);
			float rx = mid + (float)s.Rudder * 50f;
			using (SolidBrush rb = new SolidBrush(A(Theme.Cyan, 0.75)))
			{
				g.FillRectangle(rb, Math.Min(mid, rx), 31f, Math.Abs(rx - mid), 6f);
			}
			using (SolidBrush tick = new SolidBrush(Color.White))
			{
				g.FillRectangle(tick, mid - 0.75f, 27f, 1.5f, 14f);
			}
		}
		// Two lines, or a single one centered when only one trim is shown.
		if (s.ShowPitchTrim && s.ShowRollTrim)
		{
			Text(g, "P TRIM " + Percent(s.PitchTrim), 12f, Theme.Amber, trimX, 25f, outlined: o);
			Text(g, "R TRIM " + Percent(s.RollTrim), 12f, Theme.Amber, trimX, 43f, outlined: o);
		}
		else if (s.ShowPitchTrim || s.ShowRollTrim)
		{
			Text(g, s.ShowPitchTrim ? ("P TRIM " + Percent(s.PitchTrim)) : ("R TRIM " + Percent(s.RollTrim)), 12f, Theme.Amber, trimX, 34f, outlined: o);
		}
	}

	// 260 x 284: the original HUD, now with roll trim and ROLL L/R instead of BANK (it shows the stick, not attitude),
	// and a rudder ball under the ring, like the inclinometer of a turn coordinator. 270 high without the rudder.
	// Without numbers the trim and rudder values go; the ring's trim marks and the ball still show them. The sky and
	// ground tint inside the ring is part of the instrument, so it stays whatever the Background.
	private static void DrawClassic(Graphics g, HudLook look, HudState s)
	{
		bool numbers = look.Numbers;
		bool o = look.Outlined;
		float pad = 12f;
		float pw = 236f;
		float ph = ClassicPanelHeight(s);
		float cx = 130f;
		float cy = 130f;
		float r = 112f;
		if (look.Background > 0)
		{
			FillRounded(g, Color.FromArgb(PanelAlpha(look, 200), 12, 14, 20), pad, pad, pw, ph, 18f);
		}
		using (GraphicsPath clip = new GraphicsPath())
		{
			clip.AddEllipse(cx - r, cy - r, r * 2f, r * 2f);
			GraphicsState saved = g.Save();
			g.SetClip(clip, CombineMode.Intersect);
			g.TranslateTransform(cx, cy + (float)(s.Pitch * (double)r * 0.65));
			g.RotateTransform((float)((0.0 - s.Roll) * 45.0));
			using (SolidBrush sky = new SolidBrush(Color.FromArgb(77, 60, 130, 200)))
			{
				g.FillRectangle(sky, (0f - r) * 1.6f, (0f - r) * 1.6f, r * 3.2f, r * 1.6f);
			}
			using (SolidBrush ground = new SolidBrush(Color.FromArgb(77, 120, 80, 40)))
			{
				g.FillRectangle(ground, (0f - r) * 1.6f, 0f, r * 3.2f, r * 1.6f);
			}
			using (Pen hz = new Pen(Color.FromArgb(220, 0, 220, 255), 1.6f))
			{
				g.DrawLine(hz, (0f - r) * 1.6f, 0f, r * 1.6f, 0f);
			}
			g.Restore(saved);
		}
		if (o)
		{
			using Pen ringEdge = new Pen(OutlineColor, 3.6f);
			g.DrawEllipse(ringEdge, cx - r, cy - r, r * 2f, r * 2f);
		}
		using (Pen ring = new Pen(o ? Color.FromArgb(220, 90, 150, 170) : Color.FromArgb(120, 90, 150, 170), 1.4f))
		{
			g.DrawEllipse(ring, cx - r, cy - r, r * 2f, r * 2f);
		}
		PointF[] symbol = new PointF[5]
		{
			new PointF(cx - 26f, cy),
			new PointF(cx - 8f, cy),
			new PointF(cx, cy + 7f),
			new PointF(cx + 8f, cy),
			new PointF(cx + 26f, cy)
		};
		if (o)
		{
			using Pen acpEdge = new Pen(OutlineColor, 4.7f)
			{
				LineJoin = LineJoin.Round,
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			g.DrawLines(acpEdge, symbol);
		}
		using (Pen acp = new Pen(Color.White, 2.2f))
		{
			g.DrawLines(acp, symbol);
		}
		using (Pen tp = new Pen(Theme.Amber, 2f))
		{
			if (s.ShowPitchTrim)
			{
				float ty = cy + (float)s.PitchTrim * r;
				Under(g, o, 2f, cx + r - 14f, ty, cx + r - 2f, ty);
				g.DrawLine(tp, cx + r - 14f, ty, cx + r - 2f, ty);
			}
			if (s.ShowRollTrim)
			{
				float tx = cx + (float)s.RollTrim * r;
				Under(g, o, 2f, tx, cy + r - 14f, tx, cy + r - 2f);
				g.DrawLine(tp, tx, cy + r - 14f, tx, cy + r - 2f);
			}
		}
		float dx = cx + (float)s.Roll * r;
		float dy = cy + (float)s.Pitch * r;
		Dot(g, Color.FromArgb(70, 120, 255, 140), dx, dy, 11f);
		Dot(g, s.Active ? Theme.Green : Color.FromArgb(190, 195, 200), dx, dy, 5f, o);
		if (s.ShowRudder)
		{
			float ra = r + 14f;
			float span = 22f;
			RectangleF arc = new RectangleF(cx - ra, cy - ra, ra * 2f, ra * 2f);
			using (Pen tube = new Pen(o ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(34, 255, 255, 255), 11f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			})
			{
				g.DrawArc(tube, arc, 90f - span, span * 2f);
			}
			using (Pen wire = new Pen(Color.FromArgb(150, 255, 255, 255), 1.2f))
			{
				g.DrawLine(wire, cx - 6.5f, cy + ra - 8f, cx - 6.5f, cy + ra + 8f);
				g.DrawLine(wire, cx + 6.5f, cy + ra - 8f, cx + 6.5f, cy + ra + 8f);
			}
			double ba = (90.0 - Math.Clamp(s.Rudder, -1.0, 1.0) * (double)span) * Math.PI / 180.0;
			float bx = cx + ra * (float)Math.Cos(ba);
			float by = cy + ra * (float)Math.Sin(ba);
			if (s.RudderOn && s.Active)
			{
				Dot(g, A(Theme.Cyan, 0.25), bx, by, 8.5f);
			}
			Dot(g, (!s.RudderOn) ? Color.FromArgb(90, 160, 168, 178) : (s.Active ? A(Theme.Cyan, 0.95) : Color.FromArgb(200, 160, 168, 178)), bx, by, 4.5f, o);
			if (!s.RudderOn)
			{
				Text(g, "RUD OFF", 11f, Color.FromArgb(204, 220, 225, 230), pad + pw - 8f, pad + ph - 10f, StringAlignment.Far, o);
			}
			else if (numbers)
			{
				Text(g, "RUD " + Percent(s.Rudder), 11f, A(Theme.Cyan, 0.9), pad + pw - 8f, pad + ph - 10f, StringAlignment.Far, o);
			}
		}
		double off = s.Roll - s.RollTrim;
		string roll = ((Math.Abs(off) < 0.03) ? "ROLL CTR" : ((off > 0.0) ? "ROLL R" : "ROLL L"));
		Text(g, roll, 11f, A(Theme.Cyan, 0.9), pad + 8f, pad + ph - 10f, outlined: o);
		if (look.Status)
		{
			Text(g, s.Active ? "ACTIVE" : (s.Freelook ? "FREELOOK" : "STANDBY"), 11f, s.Active ? A(Theme.Green, 0.9) : (s.Freelook ? TextColor(active: false) : Color.FromArgb(230, 200, 120, 120)), pad + pw - 8f, pad + 16f, StringAlignment.Far, o);
		}
		string trim = (numbers ? TrimText(s, " ") : "");
		if (trim.Length > 0)
		{
			Text(g, "TRIM " + trim, 11f, A(Theme.Amber, 0.9), pad + 8f, pad + 16f, outlined: o);
		}
	}
}
