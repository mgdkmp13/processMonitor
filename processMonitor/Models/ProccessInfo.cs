using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

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

        // Chart data
        public List<PerformanceData> PerformanceSamples { get; set; } = new();
        
        private ObservableCollection<ISeries>? _chartSeries;
        public ObservableCollection<ISeries>? ChartSeries
        {
            get => _chartSeries;
            set
            {
                if (_chartSeries != value)
                {
                    _chartSeries = value;
                    OnPropertyChanged(nameof(ChartSeries));
                }
            }
        }

        private Axis[]? _chartXAxes;
        public Axis[]? ChartXAxes
        {
            get => _chartXAxes;
            set
            {
                if (_chartXAxes != value)
                {
                    _chartXAxes = value;
                    OnPropertyChanged(nameof(ChartXAxes));
                }
            }
        }

        private Axis[]? _chartYAxes;
        public Axis[]? ChartYAxes
        {
            get => _chartYAxes;
            set
            {
                if (_chartYAxes != value)
                {
                    _chartYAxes = value;
                    OnPropertyChanged(nameof(ChartYAxes));
                }
            }
        }

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
