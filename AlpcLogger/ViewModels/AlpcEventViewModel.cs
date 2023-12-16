using AlpcLogger.Models;
using ControlzEx.Standard;
using DebugHelp;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AlpcLogger.ViewModels
{
  internal class AlpcEventViewModel
  {
    internal AlpcEvent Event { get; }
    public int Index { get; }

    public AlpcEventViewModel(AlpcEvent evt, int index)
    {
      Event = evt;
      Index = index;
    }

    public Guid ActivityID => Event.ActivityID;

    public string ProcessName => Event.ProcessName;
    public int ProcessId => Event.ProcessId;
    public int ThreadId => Event.ThreadId;
    public int MessageId => Event.MessageId;
    public DateTime Time => Event.Time;
    public AlpcEventType Type => Event.Type;
    private CallStack _stack;
    public CallStack Stack => _stack ?? (_stack = BuildStack(Event.Stack));

    [DllImport("dbghelp", CharSet = CharSet.Unicode, EntryPoint = "SymGetModuleBase64", ExactSpelling = true, SetLastError = true)]
    public static extern ulong SymGetModuleBase64(IntPtr hProcess, ulong dwAddr);

    private CallStack BuildStack(ulong[] stack)
    {
      if (stack == null)
        return null;

      StackFrame[] frames;
      using (var handler = SymbolHandler.TryCreateFromProcess(ProcessId, SymbolOptions.Include32BitModules | SymbolOptions.UndecorateNames))
      {
        if (handler == null)
          frames = stack.Select(p => new StackFrame { Address = p }).ToArray();
        else
        {
          frames = new StackFrame[stack.Length];
          var symbol = new SymbolInfo();
          ulong disp;
          for (int i = 0; i < stack.Length; i++)
            if (handler.TryGetSymbolFromAddress(stack[i], ref symbol, out disp))
            {
              string ModuleName = null;
              IntPtr hProcess = new IntPtr(ProcessId);

              ulong baseadd = SymGetModuleBase64(hProcess, stack[i]);
              if (baseadd != 0)
              {
                var CurrentModules = handler.EnumModules();
                try
                {
                  var Module = CurrentModules.First(m => m.Base == baseadd);
                  if (Module != null)
                  {
                    ModuleName = Module.Name;
                  }
                }
                catch (Exception) { }
              }

              frames[i] = new StackFrame { Address = stack[i], Offset = disp, SymbolName = symbol.Name, ModuleName = ModuleName };
            }
            else
              frames[i] = new StackFrame { Address = stack[i] };
        }
        return new CallStack(Event, frames);
      }
    }

    private string Dispacement(ulong disp) => disp == 0 ? string.Empty : $"+0x{disp:X}";
  }
}