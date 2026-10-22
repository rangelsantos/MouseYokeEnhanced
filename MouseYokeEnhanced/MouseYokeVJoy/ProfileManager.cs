using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MouseYokeVJoy;

public static class ProfileManager
{
	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static string Dir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");

	private static string ActiveFile => Path.Combine(Dir, "_active.txt");

	public static void EnsureDir()
	{
		Directory.CreateDirectory(Dir);
	}

	public static string Sanitize(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Profile";
		}
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char c in invalidFileNameChars)
		{
			name = name.Replace(c, '_');
		}
		return name.Trim();
	}

	private static string PathFor(string name)
	{
		return Path.Combine(Dir, Sanitize(name) + ".json");
	}

	public static List<string> List()
	{
		EnsureDir();
		return Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static bool Exists(string name)
	{
		return File.Exists(PathFor(name));
	}

	public static Profile Load(string name)
	{
		try
		{
			string p = PathFor(name);
			if (File.Exists(p))
			{
				Profile prof = JsonSerializer.Deserialize<Profile>(File.ReadAllText(p));
				if (prof != null)
				{
					prof.Name = name;
					return prof;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static void Save(Profile prof)
	{
		EnsureDir();
		try
		{
			File.WriteAllText(PathFor(prof.Name), JsonSerializer.Serialize(prof, Opts));
		}
		catch
		{
		}
	}

	public static void Delete(string name)
	{
		try
		{
			string p = PathFor(name);
			if (File.Exists(p))
			{
				File.Delete(p);
			}
		}
		catch
		{
		}
	}

	public static string GetActive()
	{
		try
		{
			if (File.Exists(ActiveFile))
			{
				return File.ReadAllText(ActiveFile).Trim();
			}
		}
		catch
		{
		}
		return null;
	}

	public static void SetActive(string name)
	{
		EnsureDir();
		try
		{
			File.WriteAllText(ActiveFile, name);
		}
		catch
		{
		}
	}

	public static Profile Clone(Profile p, string newName)
	{
		Profile? profile = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p));
		profile.Name = newName;
		return profile;
	}

	public static Profile Bootstrap()
	{
		EnsureDir();
		List<string> names = List();
		if (names.Count == 0)
		{
			Profile profile = new Profile
			{
				Name = "Default"
			};
			MigrateOldBindings(profile);
			Save(profile);
			SetActive("Default");
			return profile;
		}
		string active = GetActive();
		if (active == null || !names.Contains(active, StringComparer.OrdinalIgnoreCase))
		{
			active = names[0];
		}
		Profile profile2 = Load(active) ?? new Profile
		{
			Name = active
		};
		SetActive(profile2.Name);
		return profile2;
	}

	private static void MigrateOldBindings(Profile target)
	{
		try
		{
			string old = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bindings.cfg");
			if (!File.Exists(old))
			{
				return;
			}
			string[] array = File.ReadAllLines(old);
			for (int i = 0; i < array.Length; i++)
			{
				string[] p = array[i].Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length == 0)
				{
					continue;
				}
				if (p[0] == "BTN" && p.Length >= 3 && int.TryParse(p[1], out var vk) && int.TryParse(p[2], out var btn))
				{
					target.Buttons.Add(new ButtonBinding
					{
						Vk = vk,
						Button = btn
					});
				}
				else if (p[0] == "RUDDER" && p.Length >= 5)
				{
					target.RudderEnabled = p[1] == "1";
					if (int.TryParse(p[2], out var lv))
					{
						target.RudderLeftVk = lv;
					}
					if (int.TryParse(p[3], out var rv))
					{
						target.RudderRightVk = rv;
					}
					if (int.TryParse(p[4], out var rt))
					{
						target.RudderRatePct = rt;
					}
				}
			}
		}
		catch
		{
		}
	}
}
