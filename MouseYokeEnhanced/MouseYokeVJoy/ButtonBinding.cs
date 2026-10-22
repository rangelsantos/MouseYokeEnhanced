using System.Text.Json.Serialization;

namespace MouseYokeVJoy;

public class ButtonBinding
{
	[JsonIgnore]
	public bool Pressed;

	public int Vk { get; set; }

	public int Button { get; set; }
}
