using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace processMonitor.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double MemoryMB { get; set; }
        public int ThreadCount { get; set; }
        
        private ProcessPriorityClass _priority;
        public ProcessPriorityClass Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }
        
        private bool _isTracked;
        public bool IsTracked
        {
            get => _isTracked;
            set
            {
                if (_isTracked != value)
                {
                    _isTracked = value;
                    OnPropertyChanged(nameof(IsTracked));
                }
            }
        }

        private DateTime? _trackingStartedAt;
        public DateTime? TrackingStartedAt
        {
            get => _trackingStartedAt;
            set
            {
                if (_trackingStartedAt != value)
                {
                    _trackingStartedAt = value;
                    OnPropertyChanged(nameof(TrackingStartedAt));
                }
            }
        }
        
        public ObservableCollection<ThreadInfo> Threads { get; set; } = new();
        public ObservableCollection<ModuleInfo> Modules { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ThreadInfo
    {
        public int Id { get; set; }
        public string State { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }

    public class ModuleInfo
    {
        public string ModuleName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
