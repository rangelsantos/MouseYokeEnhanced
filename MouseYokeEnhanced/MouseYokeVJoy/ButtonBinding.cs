using System.Text.Json.Serialization;

namespace MouseYokeVJoy;

public class ButtonBinding
{
	[JsonIgnore]
	public bool Pressed;

	public int Vk { get; set; }

	public int Button { get; set; }

	// Optional name shown on the Keys tab ("Landing gear"); old profiles load with an empty one.
	public string Label { get; set; } = "";
}
