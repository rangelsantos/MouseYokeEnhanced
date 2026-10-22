using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace MouseYokeVJoy;

public class Profile
{
	[JsonIgnore]
	public bool RudderLeftHeld;

	[JsonIgnore]
	public bool RudderRightHeld;

	[JsonIgnore]
	public double RudderValue;

	public string Name { get; set; } = "Default";

	public int RollSens { get; set; } = 55;

	public int PitchSens { get; set; } = 45;

	public int Deadzone { get; set; } = 4;

	public int Expo { get; set; } = 40;

	public int Smoothing { get; set; } = 15;

	public int TrimRate { get; set; } = 25;

	public bool InvertPitch { get; set; }

	public bool InvertRoll { get; set; }

	public bool HideCursor { get; set; } = true;

	public bool SelfCenter { get; set; }

	public int CenterSpeed { get; set; } = 120;

	public List<ButtonBinding> Buttons { get; set; } = new List<ButtonBinding>();

	public bool RudderEnabled { get; set; }

	public int RudderLeftVk { get; set; } = 188;

	public int RudderRightVk { get; set; } = 190;

	public int RudderRatePct { get; set; } = 60;

	public static string KeyName(int vk)
	{
		Keys k = (Keys)vk;
		return k switch
		{
			Keys.Oemcomma => "Comma ,", 
			Keys.OemPeriod => "Period .", 
			Keys.OemQuestion => "Slash /", 
			Keys.OemSemicolon => "Semicolon ;", 
			Keys.Oemtilde => "Backtick `", 
			Keys.OemMinus => "Minus -", 
			Keys.Oemplus => "Equals =", 
			_ => k.ToString(), 
		};
	}
}
