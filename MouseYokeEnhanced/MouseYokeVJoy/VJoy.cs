using System.Runtime.InteropServices;

namespace MouseYokeVJoy;

public static class VJoy
{
	public const uint HID_USAGE_X = 48u;

	public const uint HID_USAGE_Y = 49u;

	public const uint HID_USAGE_Z = 50u;

	public const int AxisMin = 1;

	public const int AxisMax = 32768;

	public const int AxisCenter = 16384;

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool vJoyEnabled();

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern VjdStat GetVJDStatus(uint rID);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool AcquireVJD(uint rID);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void RelinquishVJD(uint rID);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool ResetVJD(uint rID);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool GetVJDAxisExist(uint rID, uint Axis);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int GetVJDButtonNumber(uint rID);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool SetAxis(int Value, uint rID, uint Axis);

	[DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool SetBtn(bool Value, uint rID, byte nBtn);
}
