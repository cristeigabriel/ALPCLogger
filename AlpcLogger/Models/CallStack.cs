using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlpcLogger.Models
{
  internal class StackFrame
  {
    public string ModuleName { get; set; }
    public string SymbolName { get; set; }
    public ulong Offset { get; set; }
    public ulong Address { get; set; }

    public override string ToString()
    {
      var value = "";
      if (ModuleName != null)
        value += $"{ModuleName} ";
      value += $"0x{Address:X}";
      if (SymbolName == null)
        return value;
      value += $" {SymbolName}";
      if (Offset != 0)
        value += $"+0x{Offset:X}";
      return value;
    }
  }

  internal class CallStack
  {
    public AlpcEvent Event { get; }
    public StackFrame[] Frames { get; }

    public CallStack(AlpcEvent @event, StackFrame[] frames)
    {
      Event = @event;
      Frames = frames;
    }
  }
}