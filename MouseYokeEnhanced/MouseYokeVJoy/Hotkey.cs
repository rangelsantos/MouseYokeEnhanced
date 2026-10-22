using System;

namespace MouseYokeVJoy;

// A rebindable keyboard command. The key itself is stored on the Profile, so each aircraft
// keeps its own; 0 means unassigned. Defaults come from a fresh Profile's initializers.
public sealed class Hotkey
{
	private static readonly Profile Defaults = new Profile();

	public static readonly Hotkey[] All = new Hotkey[8]
	{
		new Hotkey("FLIGHT", "Fly / standby", (Profile p) => p.ToggleVk, (Profile p, int vk) => p.ToggleVk = vk),
		new Hotkey("TRIM", "Pitch trim up", (Profile p) => p.TrimUpVk, (Profile p, int vk) => p.TrimUpVk = vk),
		new Hotkey("TRIM", "Pitch trim down", (Profile p) => p.TrimDownVk, (Profile p, int vk) => p.TrimDownVk = vk),
		new Hotkey("TRIM", "Roll trim left", (Profile p) => p.TrimLeftVk, (Profile p, int vk) => p.TrimLeftVk = vk),
		new Hotkey("TRIM", "Roll trim right", (Profile p) => p.TrimRightVk, (Profile p, int vk) => p.TrimRightVk = vk),
		new Hotkey("TRIM", "Reset trim", (Profile p) => p.TrimResetVk, (Profile p, int vk) => p.TrimResetVk = vk),
		new Hotkey("RUDDER", "Rudder left", (Profile p) => p.RudderLeftVk, (Profile p, int vk) => p.RudderLeftVk = vk),
		new Hotkey("RUDDER", "Rudder right", (Profile p) => p.RudderRightVk, (Profile p, int vk) => p.RudderRightVk = vk)
	};

	private readonly Func<Profile, int> get;

	private readonly Action<Profile, int> set;

	public string Group { get; }

	public string Label { get; }

	public int DefaultVk => get(Defaults);

	private Hotkey(string group, string label, Func<Profile, int> get, Action<Profile, int> set)
	{
		Group = group;
		Label = label;
		this.get = get;
		this.set = set;
	}

	public int Get(Profile p)
	{
		return get(p);
	}

	public void Set(Profile p, int vk)
	{
		set(p, vk);
	}
}
