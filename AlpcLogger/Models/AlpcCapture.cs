﻿using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlpcLogger.Models
{
  internal class AlpcCapture : IDisposable
  {
    private List<AlpcMessage> _messages = new List<AlpcMessage>(1 << 16);
    private TraceEventSession _session;
    private List<ALPCSendMessageTraceData> _sendMessages = new List<ALPCSendMessageTraceData>(32);
    private List<AlpcEvent> _events = new List<AlpcEvent>(512);
    private ConcurrentDictionary<int, AlpcEvent> _threadToEvent = new ConcurrentDictionary<int, AlpcEvent>(2, 64);

    public bool IsRunning { get; set; }

    public IReadOnlyList<AlpcMessage> GetMessages() => _messages.ToList();

    public IReadOnlyList<AlpcEvent> GetEventsAndClear()
    {
      lock (_events)
      {
        var events = _events.ToList();
        _events.Clear();
        return events;
      }
    }

    public IReadOnlyList<AlpcMessage> GetMessagesAndClear()
    {
      lock (_messages)
      {
        var result = _messages.ToList();
        _messages.Clear();
        return result;
      }
    }

    public void Dispose()
    {
      IsRunning = false;
      _session.Dispose();
    }

    public void Start()
    {
      var version = Environment.OSVersion.Version;
      _session = new TraceEventSession(version.Major == 6 && version.Minor == 1 ? KernelTraceEventParser.KernelSessionName : "ALPCLogger");
      _session.StopOnDispose = true;
      _session.BufferSizeMB = 64;

      _session.EnableKernelProvider(KernelTraceEventParser.Keywords.AdvancedLocalProcedureCalls,
        KernelTraceEventParser.Keywords.AdvancedLocalProcedureCalls);

      var parser = new KernelTraceEventParser(_session.Source);
      parser.ALPCReceiveMessage += Parser_ALPCReceiveMessage;
      parser.ALPCSendMessage += Parser_ALPCSendMessage;
      parser.StackWalkStack += Parser_StackWalkStack;
      //parser.ALPCWaitForReply += Parser_ALPCWaitForReply;
      //parser.ALPCUnwait += Parser_ALPCUnwait;
      _session.Source.Process();
    }

    private void Parser_StackWalkStack(StackWalkStackTraceData obj)
    {
      AlpcEvent evt;
      if (_threadToEvent.TryRemove(obj.ThreadID, out evt))
      {
        evt.Stack = Enumerable.Range(0, obj.FrameCount).Select(i => obj.InstructionPointer(i)).ToArray();
      }
    }

    public void Pause()
    {
      IsRunning = false;
    }

    public void Run()
    {
      IsRunning = true;
    }

    private void AddEvent(AlpcEvent evt)
    {
      if (!IsRunning)
        return;

      lock (_events)
      {
        _events.Add(evt);
        if (_events.Count > 10000)
          _events.RemoveRange(0, 100);
      }
      _threadToEvent.TryAdd(evt.ThreadId, evt);
    }

    private void Parser_ALPCUnwait(ALPCUnwaitTraceData obj)
    {
      AddEvent(new AlpcEvent(obj)
      {
        Type = AlpcEventType.Unwait,
      });
    }

    private void Parser_ALPCWaitForReply(ALPCWaitForReplyTraceData obj)
    {
      AddEvent(new AlpcEvent(obj)
      {
        Type = AlpcEventType.WaitForReply,
        MessageId = obj.MessageID,
      });
    }

    private void Parser_ALPCSendMessage(ALPCSendMessageTraceData obj)
    {
      if (!IsRunning)
        return;

      AddEvent(new AlpcEvent(obj)
      {
        Type = AlpcEventType.SendMessage,
        MessageId = obj.MessageID,
      });

      lock (_sendMessages)
      {
        _sendMessages.Add((ALPCSendMessageTraceData)obj.Clone());
      }
    }

    private void Parser_ALPCReceiveMessage(ALPCReceiveMessageTraceData obj)
    {
      if (!IsRunning)
        return;

      AddEvent(new AlpcEvent(obj)
      {
        Type = AlpcEventType.ReceiveMessage,
        MessageId = obj.MessageID,
      });

      ALPCSendMessageTraceData source;
      lock (_sendMessages)
      {
        source = _sendMessages.FirstOrDefault(msg => msg.MessageID == obj.MessageID);
      }
      if (source == null)
      {
        //Console.WriteLine($"Receive without Send {obj.ProcessName} ({obj.ProcessID}) msg: {obj.MessageID}");
        return;
      }

      var message = new AlpcMessage
      {
        SourceProcess = source.ProcessID,
        SourceProcessName = source.ProcessName,
        TargetProcess = obj.ProcessID,
        TargetProcessName = obj.ProcessName,
        MessageId = obj.MessageID,
        SourceThread = source.ThreadID,
        TargetThread = obj.ThreadID,
        SendTime = source.TimeStamp,
        ReceiveTime = obj.TimeStamp,
      };
      lock (_messages)
      {
        _messages.Add(message);
      }
      _sendMessages.Remove(source);
      //Dump(message);
    }

    private void Dump(AlpcMessage message)
    {
      Console.WriteLine($"{message.SourceProcessName} ({message.SourceProcess} TID={message.SourceThread}) -> {message.MessageId}" +
        $" -> {message.TargetProcessName} ({message.TargetProcess})");
    }
  }
}