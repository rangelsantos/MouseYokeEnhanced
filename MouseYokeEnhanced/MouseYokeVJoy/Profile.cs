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

	public int ToggleVk { get; set; } = 120;

	public int TrimUpVk { get; set; } = 33;

	public int TrimDownVk { get; set; } = 34;

	public int TrimLeftVk { get; set; } = 45;

	public int TrimRightVk { get; set; } = 46;

	public int TrimResetVk { get; set; } = 36;

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
			Keys.None => "Not set",
			Keys.PageUp => "Page Up",
			Keys.PageDown => "Page Down",
			Keys.Back => "Backspace",
			Keys.Return => "Enter",
			Keys.Capital => "Caps Lock",
			>= Keys.D0 and <= Keys.D9 => ((char)vk).ToString(),
			Keys.OemOpenBrackets => "Bracket [",
			Keys.OemCloseBrackets => "Bracket ]",
			Keys.OemPipe => "Backslash \\",
			Keys.OemQuotes => "Quote '",
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
