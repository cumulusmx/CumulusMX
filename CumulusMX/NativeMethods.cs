
using System.Runtime.InteropServices;
using System;

namespace CumulusMX
{
	internal static class SafeNativeMethods
	{
		[DllImport("libc")]
		internal static extern int uname(IntPtr buf);

		[DllImport("Kernel32")]
		internal static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
	}
}
