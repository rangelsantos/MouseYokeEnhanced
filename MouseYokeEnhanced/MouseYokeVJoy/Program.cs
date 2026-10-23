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
		if (!VJoyInterfaceLoads())
		{
			return;
		}
		Application.Run(new MainForm());
	}

	// Without vJoyInterface.dll the first vJoy call throws and the app would just vanish.
	private static bool VJoyInterfaceLoads()
	{
		try
		{
			VJoy.vJoyEnabled();
			return true;
		}
		catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException || ex is EntryPointNotFoundException)
		{
			MessageBox.Show("MouseYoke can't start because vJoyInterface.dll is missing or damaged.\n\nPut vJoyInterface.dll back in the MouseYoke folder (it comes with the app). If you haven't installed vJoy yet, get it from:\nhttps://github.com/jshafer817/vJoy/releases", "MouseYokeEnhanced", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return false;
		}
	}
}
