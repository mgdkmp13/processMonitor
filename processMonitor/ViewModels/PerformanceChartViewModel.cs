using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using processMonitor.Commands;
using processMonitor.Models;

namespace processMonitor.ViewModels
{
    public class PerformanceChartViewModel : INotifyPropertyChanged
    {
        private readonly MonitoredProcess _process;
        private bool _isMonitoring;
        private double _currentMemory;
        private double _currentCpu;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PerformanceChartViewModel(MonitoredProcess process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            
            ProcessName = _process.Name;
            
            InitializeChart();
            UpdateChartData();
            
            ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring, null);
        }

        public string ProcessName { get; }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                _isMonitoring = value;
                OnPropertyChanged(nameof(IsMonitoring));
            }
        }

        public double CurrentMemory
        {
            get => _currentMemory;
            set
            {
                _currentMemory = value;
                OnPropertyChanged(nameof(CurrentMemory));
            }
        }

        public double CurrentCpu
        {
            get => _currentCpu;
            set
            {
                _currentCpu = value;
                OnPropertyChanged(nameof(CurrentCpu));
            }
        }

        public ObservableCollection<ISeries> Series { get; } = new();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();

        public ICommand ToggleMonitoringCommand { get; }

        private void InitializeChart()
        {
            XAxes = new[]
            {
                new Axis
                {
                    Name = "Time",
                    NamePaint = new SolidColorPaint(SKColors.Black),
                    LabelsPaint = new SolidColorPaint(SKColors.Black),
                    TextSize = 12,
                    Labeler = value => TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss")
                }
            };

            YAxes = new[]
            {
                new Axis
                {
                    Name = "Memory (MB)",
                    NamePaint = new SolidColorPaint(SKColors.Red),
                    LabelsPaint = new SolidColorPaint(SKColors.Red),
                    TextSize = 12,
                    Position = LiveChartsCore.Measure.AxisPosition.Start,
                    MinLimit = 0
                },
                new Axis
                {
                    Name = "CPU %",
                    NamePaint = new SolidColorPaint(SKColors.Blue),
                    LabelsPaint = new SolidColorPaint(SKColors.Blue),
                    TextSize = 12,
                    Position = LiveChartsCore.Measure.AxisPosition.End,
                    MinLimit = 0,
                    MaxLimit = 100
                }
            };
        }

        private void UpdateChartData()
        {
            Series.Clear();

            if (!_process.MemorySamples.Any())
            {
                return;
            }

            var startTime = _process.StartedAt;
            
            // Memory series
            var memoryValues = _process.MemorySamples
                .Select(s => new
                {
                    X = (s.time - startTime).TotalSeconds,
                    Y = s.memory
                })
                .OrderBy(p => p.X)
                .ToArray();

            Series.Add(new LineSeries<double>
            {
                Name = "Memory (MB)",
                Values = memoryValues.Select(v => v.Y).ToArray(),
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                GeometrySize = 4,
                GeometryFill = new SolidColorPaint(SKColors.Red),
                GeometryStroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 2 },
                ScalesYAt = 0
            });

            // Update current values
            if (memoryValues.Any())
            {
                CurrentMemory = memoryValues.Last().Y;
            }

            // For now, CPU is not tracked in MemorySamples, so we'll show 0
            // You can extend MonitoredProcess to track CPU as well
            CurrentCpu = 0;
        }

        public void RefreshData()
        {
            UpdateChartData();
        }

        private void ToggleMonitoring(object parameter)
        {
            IsMonitoring = !IsMonitoring;
            
            if (IsMonitoring)
            {
                // Start monitoring logic here if needed
            }
            else
            {
                // Stop monitoring logic here if needed
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
