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

	public bool Spring;

	public bool RudderOn;

	public string ToggleKey;
}

// Draws every HUD style in its own logical units; callers scale the Graphics to the size they want.
internal static class HudRenderer
{
	private static readonly string FontName = (FontFamily.Families.Any((FontFamily f) => f.Name == "Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : "Segoe UI Semibold");

	private static readonly Dictionary<float, Font> Fonts = new Dictionary<float, Font>();

	private static readonly StringFormat Typographic = StringFormat.GenericTypographic;

	public const int MonitorWidth = 240;

	public const int MonitorHeight = 250;

	public static Size LogicalSize(HudStyle style, bool numbers)
	{
		return style switch
		{
			HudStyle.Minimal => new Size(160, 160),
			HudStyle.Strip => new Size(360, 58),
			HudStyle.Classic => new Size(260, 284),
			_ => new Size(240, numbers ? 280 : 260),
		};
	}

	public static void Draw(Graphics g, HudStyle style, HudState s, bool numbers)
	{
		switch (style)
		{
		case HudStyle.Minimal:
			DrawMinimal(g, s);
			break;
		case HudStyle.Strip:
			DrawStrip(g, s);
			break;
		case HudStyle.Classic:
			DrawClassic(g, s);
			break;
		default:
			DrawSidestick(g, s, panel: true, labels: true, numbers);
			break;
		}
	}

	// The Fly tab monitor: the sidestick drawing without panel or text, 240 x 250.
	public static void DrawMonitor(Graphics g, HudState s)
	{
		DrawSidestick(g, s, panel: false, labels: false, numbers: false);
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

	// Text placed on its baseline, like a canvas fillText.
	private static void Text(Graphics g, string text, float px, Color color, float x, float baseline, StringAlignment align = StringAlignment.Near)
	{
		Font f = F(px);
		float ascent = px * (float)f.FontFamily.GetCellAscent(f.Style) / (float)f.FontFamily.GetEmHeight(f.Style);
		float left = x;
		if (align != StringAlignment.Near)
		{
			float w = g.MeasureString(text, f, PointF.Empty, Typographic).Width;
			left = ((align == StringAlignment.Far) ? (x - w) : (x - w / 2f));
		}
		using SolidBrush b = new SolidBrush(color);
		g.DrawString(text, f, b, left, baseline - ascent, Typographic);
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

	private static void Brackets(Graphics g, float x, float y, float s, float len, Color color, float width)
	{
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
	private static void Pointer(Graphics g, float x, float y, bool left, float s, Color color)
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
		using SolidBrush b = new SolidBrush(color);
		g.FillPolygon(b, pts);
	}

	private static void Dot(Graphics g, Color c, float x, float y, float r)
	{
		using SolidBrush b = new SolidBrush(c);
		g.FillEllipse(b, x - r, y - r, r * 2f, r * 2f);
	}

	// 240 x 280 (260 without numbers). Inspired by the Airbus sidestick indicator: the brackets are full
	// travel, the cross is the output, the amber pointers are the trims and the bottom bar is the rudder.
	private static void DrawSidestick(Graphics g, HudState s, bool panel, bool labels, bool numbers)
	{
		if (panel)
		{
			using GraphicsPath p = Rounded(new RectangleF(4f, 4f, 232f, numbers ? 272 : 252), 14f);
			using SolidBrush bg = new SolidBrush(Color.FromArgb(160, 10, 13, 19));
			using Pen edge = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
			g.FillPath(bg, p);
			g.DrawPath(edge, p);
		}
		float x0 = 28f;
		float y0 = 40f;
		float size = 160f;
		float cx = 108f;
		float cy = 120f;
		float h = 80f;
		if (labels)
		{
			Color tc = TextColor(s.Active);
			Dot(g, tc, 22f, 22f, 3.5f);
			string standby = (string.IsNullOrEmpty(s.ToggleKey) ? "STANDBY" : ("STANDBY · " + s.ToggleKey));
			Text(g, s.Active ? "ACTIVE" : standby, 12f, tc, 32f, 26f);
			Text(g, s.Spring ? "SPRING" : "DIRECT", 12f, A(Theme.Cyan, 0.85), 212f, 26f, StringAlignment.Far);
		}
		using (Pen faint = new Pen(Color.FromArgb(41, 255, 255, 255), 1f)
		{
			DashPattern = new float[2] { 3f, 4f }
		})
		{
			g.DrawLine(faint, x0 + 8f, cy, x0 + size - 8f, cy);
			g.DrawLine(faint, cx, y0 + 8f, cx, y0 + size - 8f);
		}
		if (!s.Spring && s.Deadzone > 0.0)
		{
			float r = Math.Max(3f, (float)s.Deadzone * h);
			using Pen dz = new Pen(Color.FromArgb(72, 255, 255, 255), 1f)
			{
				DashPattern = new float[2] { 1.5f, 3f }
			};
			g.DrawEllipse(dz, cx - r, cy - r, r * 2f, r * 2f);
		}
		Brackets(g, x0, y0, size, 22f, FrameColor(s.Active), 2.2f);
		using (Pen scale = new Pen(Color.FromArgb(77, 255, 255, 255), 1.2f))
		{
			g.DrawLine(scale, 200f, y0, 200f, y0 + size);
			for (int i = 0; i <= 4; i++)
			{
				float ty = y0 + (float)(i * 40);
				g.DrawLine(scale, 200f, ty, (i == 2) ? 208f : 204f, ty);
			}
			g.DrawLine(scale, x0, 210f, x0 + size, 210f);
			for (int j = 0; j <= 4; j++)
			{
				float tx = x0 + (float)(j * 40);
				g.DrawLine(scale, tx, 210f, tx, (j == 2) ? 202f : 206f);
			}
		}
		Pointer(g, 203f, cy + (float)s.PitchTrim * h, left: true, 8f, Theme.Amber);
		Pointer(g, cx + (float)s.RollTrim * h, 213f, left: false, 8f, Theme.Amber);
		FillRounded(g, Color.FromArgb(26, 255, 255, 255), x0, 228f, size, 10f, 5f);
		float rx = cx + (float)s.Rudder * h;
		using (SolidBrush rb = new SolidBrush(s.Active ? A(Theme.Cyan, 0.75) : Color.FromArgb(128, 160, 168, 178)))
		{
			g.FillRectangle(rb, Math.Min(cx, rx), 229f, Math.Abs(rx - cx), 8f);
		}
		using (SolidBrush tick = new SolidBrush(Color.White))
		{
			g.FillRectangle(tick, cx - 0.75f, 225f, 1.5f, 16f);
		}
		Text(g, "RUD", 10f, Color.FromArgb(179, 220, 225, 230), 196f, 237f);
		if (!s.RudderOn)
		{
			// Inside the right half of the empty track, clear of the roll trim pointer above the center.
			Text(g, "OFF", 9f, Color.FromArgb(204, 220, 225, 230), cx + 44f, 236.5f, StringAlignment.Center);
		}
		Cross(g, cx + (float)s.Roll * h, cy + (float)s.Pitch * h, 9f, MarkColor(s.Active), 2.6f);
		if (numbers)
		{
			Text(g, "TRIM  P " + Percent(s.PitchTrim) + "   R " + Percent(s.RollTrim), 12f, Theme.Amber, 18f, 264f);
			Text(g, "RUD " + Signed(s.Rudder), 12f, A(Theme.Cyan, 0.9), 222f, 264f, StringAlignment.Far);
		}
	}

	// 160 x 160, no panel. Every mark is drawn twice, first as a dark offset copy, so it reads on a bright sky.
	private static void DrawMinimal(Graphics g, HudState s)
	{
		DrawMinimalLayer(g, s, shadow: true);
		DrawMinimalLayer(g, s, shadow: false);
	}

	private static void DrawMinimalLayer(Graphics g, HudState s, bool shadow)
	{
		Color sh = Color.FromArgb(150, 0, 0, 0);
		float o = (shadow ? 1.2f : 0f);
		float x0 = 28f;
		float size = 104f;
		float cx = 80f;
		float cy = 80f;
		float h = 52f;
		Brackets(g, x0 + o, x0 + o, size, 16f, shadow ? sh : FrameColor(s.Active), 2.2f);
		Dot(g, shadow ? sh : TextColor(s.Active), x0 + o, 16f + o, 3.5f);
		if (!s.Active)
		{
			Text(g, "STBY", 10f, shadow ? sh : TextColor(active: false), x0 + 8f + o, 20f + o);
		}
		Pointer(g, 137f + o, cy + (float)s.PitchTrim * h + o, left: true, 7f, shadow ? sh : Theme.Amber);
		Pointer(g, cx + (float)s.RollTrim * h + o, 137f + o, left: false, 7f, shadow ? sh : Theme.Amber);
		using (Pen line = new Pen(shadow ? sh : Color.FromArgb(128, 255, 255, 255), 1.2f))
		{
			g.DrawLine(line, x0 + o, 152f + o, x0 + size + o, 152f + o);
			g.DrawLine(line, cx + o, 148f + o, cx + o, 156f + o);
		}
		Dot(g, shadow ? sh : FrameColor(s.Active), cx + (float)s.Rudder * h + o, 152f + o, 3f);
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

	// 360 x 58: a thin bar for a screen edge.
	private static void DrawStrip(Graphics g, HudState s)
	{
		FillRounded(g, Color.FromArgb(168, 10, 13, 19), 2f, 2f, 356f, 54f, 10f);
		Color tc = TextColor(s.Active);
		Dot(g, tc, 16f, 29f, 3.5f);
		Text(g, s.Active ? "ACTIVE" : "STBY", 12f, tc, 25f, 33f);
		float bx = 86f;
		float by = 8f;
		float bc = bx + 21f;
		float bcy = by + 21f;
		Brackets(g, bx, by, 42f, 8f, FrameColor(s.Active), 1.8f);
		Pointer(g, bx + 45f, bcy + (float)s.PitchTrim * 21f, left: true, 5f, Theme.Amber);
		Cross(g, bc + (float)s.Roll * 21f, bcy + (float)s.Pitch * 21f, 5f, MarkColor(s.Active), 2f);
		Text(g, s.RudderOn ? "RUD" : "RUD OFF", 10f, Color.FromArgb(179, 220, 225, 230), 150f, 24f);
		FillRounded(g, Color.FromArgb(31, 255, 255, 255), 150f, 30f, 100f, 8f, 4f);
		float rx = 200f + (float)s.Rudder * 50f;
		using (SolidBrush rb = new SolidBrush(A(Theme.Cyan, 0.75)))
		{
			g.FillRectangle(rb, Math.Min(200f, rx), 31f, Math.Abs(rx - 200f), 6f);
		}
		using (SolidBrush tick = new SolidBrush(Color.White))
		{
			g.FillRectangle(tick, 199.25f, 27f, 1.5f, 14f);
		}
		Text(g, "P TRIM " + Percent(s.PitchTrim), 12f, Theme.Amber, 266f, 25f);
		Text(g, "R TRIM " + Percent(s.RollTrim), 12f, Theme.Amber, 266f, 43f);
	}

	// 260 x 284: the original HUD, now with roll trim and ROLL L/R instead of BANK (it shows the stick, not attitude),
	// and a rudder ball under the ring, like the inclinometer of a turn coordinator.
	private static void DrawClassic(Graphics g, HudState s)
	{
		float pad = 12f;
		float pw = 236f;
		float ph = 260f;
		float cx = 130f;
		float cy = 130f;
		float r = 112f;
		FillRounded(g, Color.FromArgb(200, 12, 14, 20), pad, pad, pw, ph, 18f);
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
		using (Pen ring = new Pen(Color.FromArgb(120, 90, 150, 170), 1.4f))
		{
			g.DrawEllipse(ring, cx - r, cy - r, r * 2f, r * 2f);
		}
		using (Pen acp = new Pen(Color.White, 2.2f))
		{
			g.DrawLines(acp, new PointF[5]
			{
				new PointF(cx - 26f, cy),
				new PointF(cx - 8f, cy),
				new PointF(cx, cy + 7f),
				new PointF(cx + 8f, cy),
				new PointF(cx + 26f, cy)
			});
		}
		using (Pen tp = new Pen(Theme.Amber, 2f))
		{
			float ty = cy + (float)s.PitchTrim * r;
			g.DrawLine(tp, cx + r - 14f, ty, cx + r - 2f, ty);
			float tx = cx + (float)s.RollTrim * r;
			g.DrawLine(tp, tx, cy + r - 14f, tx, cy + r - 2f);
		}
		float dx = cx + (float)s.Roll * r;
		float dy = cy + (float)s.Pitch * r;
		Dot(g, Color.FromArgb(70, 120, 255, 140), dx, dy, 11f);
		Dot(g, s.Active ? Theme.Green : Color.FromArgb(190, 195, 200), dx, dy, 5f);
		float ra = r + 14f;
		float span = 22f;
		RectangleF arc = new RectangleF(cx - ra, cy - ra, ra * 2f, ra * 2f);
		using (Pen tube = new Pen(Color.FromArgb(34, 255, 255, 255), 11f)
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
		Dot(g, (!s.RudderOn) ? Color.FromArgb(90, 160, 168, 178) : (s.Active ? A(Theme.Cyan, 0.95) : Color.FromArgb(200, 160, 168, 178)), bx, by, 4.5f);
		double off = s.Roll - s.RollTrim;
		string roll = ((Math.Abs(off) < 0.03) ? "ROLL CTR" : ((off > 0.0) ? "ROLL R" : "ROLL L"));
		Text(g, roll, 11f, A(Theme.Cyan, 0.9), pad + 8f, pad + ph - 10f);
		Text(g, s.RudderOn ? ("RUD " + Percent(s.Rudder)) : "RUD OFF", 11f, s.RudderOn ? A(Theme.Cyan, 0.9) : Color.FromArgb(204, 220, 225, 230), pad + pw - 8f, pad + ph - 10f, StringAlignment.Far);
		Text(g, s.Active ? "ACTIVE" : "STANDBY", 11f, s.Active ? A(Theme.Green, 0.9) : Color.FromArgb(230, 200, 120, 120), pad + pw - 8f, pad + 16f, StringAlignment.Far);
		Text(g, "TRIM P " + Percent(s.PitchTrim) + " R " + Percent(s.RollTrim), 11f, A(Theme.Amber, 0.9), pad + 8f, pad + 16f);
	}
}
