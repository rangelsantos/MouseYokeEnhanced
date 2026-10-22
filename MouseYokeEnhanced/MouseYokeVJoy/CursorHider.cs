using System;
using System.Runtime.InteropServices;

namespace MouseYokeVJoy;

internal static class CursorHider
{
	private const uint SPI_SETCURSORS = 87u;

	private const uint SPIF_SENDCHANGE = 2u;

	private static readonly uint[] Ids = new uint[13]
	{
		32512u, 32513u, 32514u, 32515u, 32516u, 32642u, 32643u, 32644u, 32645u, 32646u,
		32648u, 32649u, 32650u
	};

	private static bool hidden;

	[DllImport("user32.dll")]
	private static extern bool SetSystemCursor(nint hcur, uint id);

	[DllImport("user32.dll")]
	private static extern nint CreateCursor(nint hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

	[DllImport("user32.dll")]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, nint pvParam, uint fWinIni);

	[DllImport("user32.dll")]
	private static extern nint CopyIcon(nint hIcon);

	[DllImport("user32.dll")]
	private static extern bool DestroyCursor(nint hCursor);

	public static void Hide()
	{
		if (hidden)
		{
			return;
		}
		int bytes = 128;
		byte[] andPlane = new byte[bytes];
		for (int i = 0; i < bytes; i++)
		{
			andPlane[i] = byte.MaxValue;
		}
		byte[] xorPlane = new byte[bytes];
		nint blank = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andPlane, xorPlane);
		if (blank == IntPtr.Zero)
		{
			return;
		}
		uint[] ids = Ids;
		foreach (uint id in ids)
		{
			nint copy = CopyIcon(blank);
			if (copy != IntPtr.Zero)
			{
				SetSystemCursor(copy, id);
			}
		}
		DestroyCursor(blank);
		hidden = true;
	}

	public static void Restore()
	{
		if (hidden)
		{
			SystemParametersInfo(87u, 0u, IntPtr.Zero, 2u);
			hidden = false;
		}
	}
}
