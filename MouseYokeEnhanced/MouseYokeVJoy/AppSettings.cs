using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseYokeVJoy;

// Whether a HUD element (rudder, pitch trim, roll trim) is drawn. Auto draws it when the active profile uses it.
public enum HudElementMode
{
	Auto,
	Show,
	Hide
}

// App-wide settings, the same for every aircraft profile. HUD position and size stay in overlay.cfg.
public class AppSettings
{
	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private static string PathName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

	public bool ShowHud { get; set; } = true;

	public HudStyle HudStyle { get; set; }

	public int HudOpacity { get; set; } = 90;

	public bool HudNumbers { get; set; } = true;

	// The panel behind the HUD, 0..100%; 0 draws only the marks, outlined.
	public int HudBackground { get; set; } = 100;

	public bool HudStatus { get; set; } = true;

	public bool HudFade { get; set; }

	public bool HudInStandby { get; set; }

	public HudElementMode HudRudder { get; set; } = HudElementMode.Auto;

	public HudElementMode HudPitchTrim { get; set; } = HudElementMode.Auto;

	public HudElementMode HudRollTrim { get; set; } = HudElementMode.Auto;

	public bool TopMost { get; set; } = true;

	public bool CloseToTray { get; set; }

	public bool Compact { get; set; }

	// Client height of the full window in pixels, as the user dragged it; 0 means the default.
	public int WindowHeight { get; set; }

	[JsonIgnore]
	public HudLook Look => new HudLook(HudStyle, HudNumbers, HudBackground, HudStatus);

	public static AppSettings Load()
	{
		AppSettings s = null;
		try
		{
			if (File.Exists(PathName))
			{
				s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName), Opts);
			}
		}
		catch
		{
		}
		s ??= new AppSettings();
		s.HudBackground = Math.Clamp(s.HudBackground, 0, 100);
		return s;
	}

	public void Save()
	{
		try
		{
			File.WriteAllText(PathName, JsonSerializer.Serialize(this, Opts));
		}
		catch
		{
		}
	}
}
