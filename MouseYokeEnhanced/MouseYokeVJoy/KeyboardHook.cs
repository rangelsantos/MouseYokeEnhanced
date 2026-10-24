using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseYokeVJoy;

public class KeyboardHook : IDisposable
{
	private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

	private struct KBDLLHOOKSTRUCT
	{
		public uint vkCode;

		public uint scanCode;

		public uint flags;

		public uint time;

		public nint dwExtraInfo;
	}

	private const int WH_KEYBOARD_LL = 13;

	private const int WM_KEYDOWN = 256;

	private const int WM_KEYUP = 257;

	private const int WM_SYSKEYDOWN = 260;

	private const int WM_SYSKEYUP = 261;

	private LowLevelKeyboardProc _proc;

	private nint _hook = IntPtr.Zero;

	private readonly HashSet<int> _down = new HashSet<int>();

	private readonly HashSet<int> _stale = new HashSet<int>();

	public event Action<int> KeyDown;

	public event Action<int> KeyUp;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll")]
	private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	public void Install()
	{
		_proc = HookCallback;
		using Process cur = Process.GetCurrentProcess();
		ProcessModule mod = cur.MainModule;
		try
		{
			_hook = SetWindowsHookEx(13, _proc, GetModuleHandle(mod.ModuleName), 0u);
		}
		finally
		{
			((IDisposable)mod)?.Dispose();
		}
	}

	private nint HookCallback(int nCode, nint wParam, nint lParam)
	{
		if (nCode >= 0)
		{
			int msg = ((IntPtr)wParam).ToInt32();
			int vk = (int)Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;
			switch (msg)
			{
			case 256:
			case 260:
				Press(vk);
				break;
			case 257:
			case 261:
				Release(vk);
				break;
			}
		}
		return CallNextHookEx(_hook, nCode, wParam, lParam);
	}

	// Mouse buttons come from Raw Input, not from this hook; they join the same held-key
	// tracking so commands bound to them get the same stale-key release.
	public void MouseButton(int vk, bool down)
	{
		if (down)
		{
			Press(vk);
		}
		else
		{
			Release(vk);
		}
	}

	private void Press(int vk)
	{
		if (_down.Add(vk))
		{
			KeyDown?.Invoke(vk);
		}
	}

	private void Release(int vk)
	{
		_stale.Remove(vk);
		if (_down.Remove(vk))
		{
			KeyUp?.Invoke(vk);
		}
	}

	// Fires KeyUp for keys whose key-up never reached the hook (Win+L, UAC prompt,
	// Ctrl+Alt+Del, focus on an elevated window...), so trim/rudder/buttons can't stay held.
	// A key must read as up on two consecutive calls, so this never races the hook itself.
	public void ReleaseStaleKeys()
	{
		if (_down.Count == 0)
		{
			_stale.Clear();
			return;
		}
		List<int> released = null;
		foreach (int vk in _down)
		{
			if ((GetAsyncKeyState(vk) & 0x8000) != 0)
			{
				_stale.Remove(vk);
			}
			else if (!_stale.Add(vk))
			{
				(released ??= new List<int>()).Add(vk);
			}
		}
		if (released == null)
		{
			return;
		}
		foreach (int vk in released)
		{
			_down.Remove(vk);
			_stale.Remove(vk);
			KeyUp?.Invoke(vk);
		}
	}

	public void Dispose()
	{
		if (_hook != IntPtr.Zero)
		{
			UnhookWindowsHookEx(_hook);
			_hook = IntPtr.Zero;
		}
	}
}
