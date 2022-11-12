using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GlobalInputHook.Interop
{
    internal static class Interop
    {
        // Source: https://blogs.msdn.microsoft.com/toub/2006/05/03/low-level-keyboard-hook-in-c/
        internal const int WM_KEYDOWN = 0x0100;
        internal const int WM_KEYUP = 0x0101;
        internal const int WM_SYSKEYDOWN = 0x0104;
        internal const int WM_SYSKEYUP = 0x0105;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_LBUTTONUP = 0x0202;
        internal const int WM_RBUTTONDOWN = 0x0204;
        internal const int WM_RBUTTONUP = 0x0205;
        internal const int WM_MOUSEWHEEL = 0x020A;
        internal const int WM_MOUSEMOVE = 0x0200;
        internal const int WM_MBUTTONDOWN = 0x0207;
        internal const int WM_MBUTTONUP = 0x0208;
        internal const int WM_NCXBUTTONDOWN = 0x00AB;
        internal const int WM_NCXBUTTONUP = 0x00AC;
        internal const int WM_XBUTTONDOWN = 0x020B;
        internal const int WM_XBUTTONUP = 0x020C;
        internal const int WH_KEYBOARD_LL = 13;
        internal const int WH_MOUSE_LL = 14;

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short HiWordSigned(uint n)
        {
            return (short)(((int)n >> 16) & 0xFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort HiWord(uint n)
        {
            return (ushort)((n >> 16) & 0xFFFF);
        }

        internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        internal static IntPtr SetHook(int hookType, HookProc proc)
        {
            IntPtr hook = SetWindowsHookEx(hookType, proc, GetModuleHandle("user32"), 0);
            if (hook == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception();
            }
            return hook;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook,
            HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);
    }
}
