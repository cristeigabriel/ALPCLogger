﻿using AlpcLogger.Models;
using AlpcLogger.Views;
using CsvHelper;
using CsvHelper.Configuration;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Zodiacon.WPF;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace AlpcLogger.ViewModels
{
  internal class MainViewModel : BindableBase, IDisposable
  {
    private ObservableCollection<AlpcMessageViewModel> _messages = new ObservableCollection<AlpcMessageViewModel>();
    private ObservableCollection<AlpcEventViewModel> _events = new ObservableCollection<AlpcEventViewModel>();
    private int _lastIndex = 0;

    private DispatcherTimer _messagesTimer, _eventsTimer, _eventsStackframeBuilder;
    private AlpcCapture _capture = new AlpcCapture();
    public ListCollectionView MessagesView { get; }
    public ListCollectionView EventsView { get; }

    public IList<AlpcMessageViewModel> Messages => _messages;
    public IList<AlpcEventViewModel> Events => _events;

    public readonly IUIServices UI;

    public MainViewModel(IUIServices ui)
    {
      UI = ui;

      Thread.CurrentThread.Priority = ThreadPriority.Highest;

      _messagesTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
      _messagesTimer.Tick += _timer_Tick;
      _messagesTimer.Start();

      _eventsTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1.5) };
      _eventsTimer.Tick += _timer2_Tick;
      _eventsTimer.Start();

      _eventsStackframeBuilder = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
      _eventsStackframeBuilder.Tick += _timer3_Tick;
      _eventsStackframeBuilder.Start();

      var thread = new Thread(_capture.Start);
      thread.IsBackground = true;
      thread.Priority = ThreadPriority.BelowNormal;
      thread.Start();

      MessagesView = (ListCollectionView)CollectionViewSource.GetDefaultView(Messages);
      EventsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Events);
    }

    private void _timer_Tick(object sender, EventArgs e)
    {
      _messagesTimer.Stop();
      var messages = _capture.GetMessagesAndClear();
      var count = _messages.Count;
      foreach (var msg in messages)
      {
        _messages.Add(new AlpcMessageViewModel(msg, count++));
      }

      _messagesTimer.Start();
    }

    private void _timer2_Tick(object sender, EventArgs e)
    {
      _eventsTimer.Stop();
      var events = _capture.GetEventsAndClear();
      var count = events.Count;
      foreach (var evt in events)
        _events.Add(new AlpcEventViewModel(evt, count++));

      _eventsTimer.Start();
    }

    private void _timer3_Tick(object sender, EventArgs e)
    {
      _eventsStackframeBuilder.Stop();

      Task.Run(() =>
      {
        var n = 15; // events
        if (_events.Count < (_lastIndex + n))
        {
          n = _events.Count - _lastIndex;
        }
        if (n > 0)
        {
          for (int i = _lastIndex; i < (_lastIndex + n); i++)
          {
            if (_events[i]._stack == null)
            {
              _events[i]._stack = _events[i].BuildStack();
            }
          }
          _lastIndex += n;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
          EventsView.Filter = obj =>
          {
            return SearchTextEventsViewFilter(obj) &&
            StackframeSearchEventsViewFilter(obj);
          };
        });
      });

      _eventsStackframeBuilder.Start();
    }

    private int _selectedTab = 1;

    public int SelectedTab
    {
      get { return _selectedTab; }
      set
      {
        if (SetProperty(ref _selectedTab, value))
        {
        }
      }
    }

    public void Dispose()
    {
      _capture.Dispose();
    }

    private bool _isRunning;

    public bool IsRunning
    {
      get { return _isRunning; }
      set
      {
        if (SetProperty(ref _isRunning, value))
        {
          RaisePropertyChanged(nameof(SessionState));
        }
      }
    }

    private string _searchText;
    private string _stackframeSearchText;

    private static char[] _separators = new char[] { ';', ',' };

    public string SearchText
    {
      get { return _searchText; }
      set
      {
        if (SetProperty(ref _searchText, value))
        {
          if (string.IsNullOrWhiteSpace(value))
          {
            MessagesView.Filter = null;
          }
          else
          {
            var words = value.Trim().ToLowerInvariant().Split(_separators, StringSplitOptions.RemoveEmptyEntries);
            MessagesView.Filter = obj =>
            {
              var msg = (AlpcMessageViewModel)obj;
              var src = msg.SourceProcessName.ToLowerInvariant();
              var srcPid = msg.SourceProcess.ToString();
              var tgt = msg.TargetProcessName.ToLowerInvariant();
              var tgtPid = msg.TargetProcess.ToString();
              int negates = words.Count(w => w[0] == '-');

              foreach (var text in words)
              {
                string negText;
                if (text[0] == '-' && text.Length > 2 && (src.Contains(negText = text.Substring(1).ToLowerInvariant()) || tgt.Contains(negText)))
                  return false;

                if (text[0] != '-' && (src.Contains(text) || tgt.Contains(text)))
                  return true;

                if (text[0] != '-' && (srcPid.Contains(text) || tgtPid.Contains(text)))
                  return true;
              }
              return negates == words.Length;
            };
            EventsView.Filter = obj =>
            {
              return SearchTextEventsViewFilter(obj) &&
              StackframeSearchEventsViewFilter(obj);
            };
          }
        }
      }
    }

    public bool SearchTextEventsViewFilter(object obj)
    {
      if (string.IsNullOrEmpty(_searchText))
      {
        return true;
      }
      var words = _searchText.Trim().ToLowerInvariant().Split(_separators, StringSplitOptions.RemoveEmptyEntries);
      var msg = (AlpcEventViewModel)obj;
      var src = msg.ProcessName.ToLowerInvariant();
      int negates = words.Count(w => w[0] == '-');

      foreach (var text in words)
      {
        if (text[0] == '-' && text.Length > 2 && (src.Contains(text.Substring(1).ToLowerInvariant())))
          return false;

        if (text[0] != '-' && src.Contains(text))
          return true;
      }
      return negates == words.Length;
    }

    public class Pair<T, U>
    {
      public Pair()
      {
      }

      public Pair(T first, U second)
      {
        this.Item1 = first;
        this.Item2 = second;
      }

      public T Item1 { get; set; }
      public U Item2 { get; set; }
    };

    public string StackframeSearchText
    {
      get { return _stackframeSearchText; }
      set
      {
        if (SetProperty(ref _stackframeSearchText, value))
        {
          if (!string.IsNullOrWhiteSpace(value))
          {
            EventsView.Filter = obj =>
            {
              return SearchTextEventsViewFilter(obj) &&
              StackframeSearchEventsViewFilter(obj);
            };
          }
        }
      }
    }

    public bool StackframeSearchEventsViewFilter(object obj)
    {
      if (string.IsNullOrEmpty(_stackframeSearchText))
      {
        return true;
      }

      var evt = (AlpcEventViewModel)obj;
      if (evt._stack != null)
      {
        var frames = evt._stack.Frames;

        var pass = 0;
        var wwords = _stackframeSearchText.Trim().ToLowerInvariant().Split(_separators, StringSplitOptions.RemoveEmptyEntries);
        var words = wwords.Select(x => new Pair<string, bool>(x, false));
        foreach (var frame in frames)
        {
          var modName = frame.ModuleName;
          if (!string.IsNullOrEmpty(modName))
            modName = modName.ToLowerInvariant();
          var symName = frame.SymbolName;
          if (!string.IsNullOrEmpty(symName))
            symName = symName.ToLowerInvariant();
          foreach (var word in words)
          {
            if (!string.IsNullOrEmpty(modName) && modName.Contains(word.Item1) && !word.Item2)
            {
              pass++;
              word.Item2 = true;
            }
            if (!string.IsNullOrEmpty(symName) && symName.Contains(word.Item1) && !word.Item2)
            {
              pass++;
              word.Item2 = true;
            }
            if (pass >= wwords.Length)
            {
              break;
            }
          }
          if (pass >= wwords.Length)
          {
            break;
          }
        }

        return pass >= wwords.Length;
      }
      return false;
    }

    public string SessionState => IsRunning ? "Running" : "Stopped";

    public ICommand ExitCommand => new DelegateCommand(() =>
    {
      Dispose();
      Application.Current.MainWindow.Close();
    });

    public DelegateCommandBase StartCommand => new DelegateCommand(() =>
    {
      _capture.Run();
      IsRunning = true;
    }, () => !IsRunning).ObservesProperty(() => IsRunning);

    public DelegateCommandBase StopCommand => new DelegateCommand(() =>
    {
      _capture.Pause();
      IsRunning = false;
    }, () => IsRunning).ObservesProperty(() => IsRunning);

    public ICommand FindChainsCommand => new DelegateCommand(() =>
    {
      var finder = new AlpcChainsFinder(Messages.Where(m => MessagesView.PassesFilter(m)).Select(m => m.Message).ToList());
      foreach (var chain in finder.FindAllChains())
      {
        var msg1 = chain[0];
        var msg2 = chain[1];
      }
    });

    public DelegateCommandBase SaveAllCommand => new DelegateCommand(() =>
    {
      if (Messages.Count == 0)
        return;

      DoSave(true);
    });

    private AlpcEventViewModel _selectedEvent;

    public AlpcEventViewModel SelectedEvent
    {
      get { return _selectedEvent; }
      set { SetProperty(ref _selectedEvent, value); }
    }

    public DelegateCommandBase StackCommand => new DelegateCommand(() =>
    {
      var stack = SelectedEvent.Stack;
      var vm = UI.DialogService.CreateDialog<CallStackViewModel, CallStackView>(stack);
      vm.Show();
    }, () => SelectedEvent != null && SelectedTab == 0)
      .ObservesProperty(() => SelectedEvent).ObservesProperty(() => SelectedTab);

    public ICommand ClearLogCommand => new DelegateCommand(() =>
    {
      Messages.Clear();
      Events.Clear();
    });

    public DelegateCommandBase SaveFilteredCommand => new DelegateCommand(() =>
    {
      if (MessagesView.Count == 0)
        return;

      DoSave(false);
    });

    private void DoSave(bool all)
    {
      var filename = UI.FileDialogService.GetFileForSave("CSV Files (*.csv)|*.csv|All Files|*.*");
      if (filename == null)
        return;

      SaveInternal(filename, all);
    }

    private void SaveInternal(string filename, bool all)
    {
      _messagesTimer.Stop();

      try
      {
        var config = new Configuration
        {
          IncludePrivateMembers = true,
        };

        using (var writer = new StreamWriter(filename))
        {
          var csvWriter = new CsvWriter(writer, config);
          if (all)
          {
            csvWriter.WriteRecords(_messages);
          }
          else
          {
            csvWriter.WriteHeader<AlpcMessageViewModel>();
            csvWriter.NextRecord();
            foreach (var msg in _messages)
            {
              if (MessagesView.Contains(msg))
              {
                csvWriter.WriteRecord(msg);
                csvWriter.NextRecord();
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        UI.MessageBoxService.ShowMessage(ex.Message, App.Name);
      }
      finally
      {
        _messagesTimer.Start();
      }
    }
  }
}