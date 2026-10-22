using System;
using System.Windows.Forms;

namespace MouseYokeVJoy;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new MainForm());
	}
}
