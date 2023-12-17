using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace AlpcLogger.Util
{
  public static class WindowUtils
  {
    //
    // this api literally lies
    // msdn implies some variance and state
    // binvert: "If this parameter is TRUE, the window is flashed from one state to the other. If it is FALSE, the window is returned to its original state (either active or inactive)."
    //
    // but if u open it in ida, it just does this
    //
    // bInvert ? (FLASHW_CAPTION | FLASHW_TRAY) : FLASHW_STOP
    //
    // so i named the argument accordingly
    //
    [DllImport("user32", CharSet = CharSet.Unicode, EntryPoint = "FlashWindow", ExactSpelling = true, SetLastError = false)]
    private static extern UInt32 FlashWindowApi(IntPtr handle, UInt32 flashOn);

    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
      public UInt32 cbSize;
      public IntPtr hwnd;
      public UInt32 dwFlags;
      public UInt32 uCount;
      public UInt32 dwTimeout;
    }

    [DllImport("user32", CharSet = CharSet.Unicode, EntryPoint = "FlashWindowEx", ExactSpelling = true, SetLastError = false)]
    private static extern UInt32 FlashWindowEx(ref FLASHWINFO fi);

    [DllImport("user32", CharSet = CharSet.Unicode, EntryPoint = "IsIconic", ExactSpelling = true, SetLastError = false)]
    private static extern UInt32 IsIconicApi(IntPtr hwnd);

    public enum TaskbarStates
    {
      NoProgress = 0,
      Indeterminate = 0x1,
      Normal = 0x2,
      Error = 0x4,
      Paused = 0x8
    }

    [ComImport()]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
      // ITaskbarList
      [PreserveSig]
      void HrInit();

      [PreserveSig]
      void AddTab(IntPtr hwnd);

      [PreserveSig]
      void DeleteTab(IntPtr hwnd);

      [PreserveSig]
      void ActivateTab(IntPtr hwnd);

      [PreserveSig]
      void SetActiveAlt(IntPtr hwnd);

      // ITaskbarList2
      [PreserveSig]
      void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

      // ITaskbarList3
      [PreserveSig]
      void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);

      [PreserveSig]
      void SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    [ComImport()]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance
    {
    }

    private static ITaskbarList3 taskbarInstance = (ITaskbarList3)new TaskbarInstance();
    private static bool taskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);

    public static void SetState(TaskbarStates taskbarState)
    {
      Process currentProcess = Process.GetCurrentProcess();
      IntPtr mainWindowHandle = currentProcess.MainWindowHandle;
      if (taskbarSupported) taskbarInstance.SetProgressState(mainWindowHandle, taskbarState);
    }

    public static void SetValue(double progressValue, double progressMax)
    {
      Process currentProcess = Process.GetCurrentProcess();
      IntPtr mainWindowHandle = currentProcess.MainWindowHandle;
      if (taskbarSupported) taskbarInstance.SetProgressValue(mainWindowHandle, (ulong)progressValue, (ulong)progressMax);
    }

    public static bool IsIconic()
    {
      Process currentProcess = Process.GetCurrentProcess();
      IntPtr mainWindowHandle = currentProcess.MainWindowHandle;
      return IsIconicApi(mainWindowHandle) == 1;
    }
  }
}