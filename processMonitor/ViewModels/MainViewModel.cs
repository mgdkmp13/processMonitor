using processMonitor.Commands;
using processMonitor.Models;
using processMonitor.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace processMonitor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ProcessInfo> Processes { get; }
        public ObservableCollection<MonitoredProcess> MonitoredProcesses { get; }
        public ICollectionView ProcessesView { get; }

        private ProcessInfo? _selectedProcess;
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                _selectedProcess = value;
                OnPropertyChanged(nameof(SelectedProcess));
                
                if (value != null && value.Threads.Count == 0)
                {
                    _ = LoadProcessDetailsAsync(value);
                }
            }
        }

        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                ProcessesView.Refresh();
            }
        }

        private int _refreshInterval = 5000;
        public int RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                if (value < 1000) value = 1000;
                _refreshInterval = value;
                OnPropertyChanged(nameof(RefreshInterval));
                RestartAutoRefresh();
            }
        }

        private bool _isAutoRefreshEnabled = false;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                _isAutoRefreshEnabled = value;
                OnPropertyChanged(nameof(IsAutoRefreshEnabled));
                if (value)
                    StartAutoRefresh();
                else
                    StopAutoRefresh();
            }
        }

        private int _samplingInterval = 2000;
        public int SamplingInterval
        {
            get => _samplingInterval;
            set
            {
                if (value < 500) value = 500;
                _samplingInterval = value;
                OnPropertyChanged(nameof(SamplingInterval));
            }
        }

        private bool _isRefreshing = false;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged(nameof(IsRefreshing));
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand KillCommand { get; }
        public ICommand ToggleTrackingCommand { get; }
        public ICommand ChangePriorityCommand { get; }
        public ICommand ShowMonitoredWindowCommand { get; }

        private readonly ProcessService _service = new();
        private readonly ProcessMonitorService _tracking = new();
        private readonly Dictionary<int, CancellationTokenSource> _trackingTokens = new();
        private CancellationTokenSource? _autoRefreshToken;
        private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private readonly object _processesLock = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel()
        {
            Processes = new ObservableCollection<ProcessInfo>();
            MonitoredProcesses = new ObservableCollection<MonitoredProcess>();
            
            BindingOperations.EnableCollectionSynchronization(Processes, _processesLock);
            BindingOperations.EnableCollectionSynchronization(MonitoredProcesses, _processesLock);
            
            ProcessesView = CollectionViewSource.GetDefaultView(Processes);
            ProcessesView.Filter = FilterProcesses;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            KillCommand = new RelayCommand(async p => await KillAsync((ProcessInfo)p!), p => p != null);
            ToggleTrackingCommand = new RelayCommand(p => ToggleTracking((ProcessInfo)p!), p => p != null);
            ChangePriorityCommand = new RelayCommand(async p => await ChangePriorityAsync((object[])p!), p => p != null);
            ShowMonitoredWindowCommand = new RelayCommand(_ => ShowMonitoredWindow());

            _ = RefreshAsync();
            _ = StartChartUpdateLoopAsync();
        }

        private bool FilterProcesses(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterText))
                return true;

            var process = obj as ProcessInfo;
            return process?.Name?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private void StartAutoRefresh()
        {
            if (!IsAutoRefreshEnabled) return;

            _autoRefreshToken = new CancellationTokenSource();
            _ = AutoRefreshAsync(_autoRefreshToken.Token);
        }

        private void StopAutoRefresh()
        {
            _autoRefreshToken?.Cancel();
            _autoRefreshToken?.Dispose();
            _autoRefreshToken = null;
        }

        private void RestartAutoRefresh()
        {
            if (IsAutoRefreshEnabled)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        private async Task AutoRefreshAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshInterval, token);
                    if (!token.IsCancellationRequested)
                    {
                        await RefreshAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                }
            }
        }

        private async Task StartChartUpdateLoopAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    UpdateChartsForTrackedProcesses();
                }
                catch
                {
                }
            }
        }

        private void UpdateChartsForTrackedProcesses()
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateCharts] Running update...");
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var trackedProcesses = Processes.Where(p => p.IsTracked).ToList();
                System.Diagnostics.Debug.WriteLine($"[UpdateCharts] Found {trackedProcesses.Count} tracked processes");
                
                foreach (var process in trackedProcesses)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateCharts] Checking process {process.Id} ({process.Name})");
                    
                    var monitored = MonitoredProcesses.FirstOrDefault(m => m.ProcessId == process.Id);
                    if (monitored == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateCharts] No monitored process found for {process.Id}");
                        continue;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[UpdateCharts] Monitored process found with {monitored.MemorySamples.Count} samples");
                    
                    if (monitored.MemorySamples.Any())
                    {
                        UpdateProcessChart(process, monitored);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateCharts] No samples yet for process {process.Id}");
                    }
                }
            });
        }

        private void UpdateProcessChart(ProcessInfo process, MonitoredProcess monitored)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] ProcessId: {process.Id}, Samples: {monitored.MemorySamples.Count}");
            
            if (monitored.MemorySamples.Count < 1)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Too few samples: {monitored.MemorySamples.Count}");
                return;
            }

            var startTime = monitored.StartedAt;
            var memoryValues = monitored.MemorySamples
                .Select(s => new { X = (s.time - startTime).TotalSeconds, Y = s.memory })
                .OrderBy(p => p.X)
                .ToArray();

            System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Memory values count: {memoryValues.Length}");
            if (memoryValues.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] First value: X={memoryValues[0].X:F2}, Y={memoryValues[0].Y:F2}");
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Last value: X={memoryValues.Last().X:F2}, Y={memoryValues.Last().Y:F2}");
                
                // Sprawdź zakres wartości
                var minY = memoryValues.Min(v => v.Y);
                var maxY = memoryValues.Max(v => v.Y);
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Y Range: {minY:F2} - {maxY:F2} (diff: {(maxY - minY):F2})");
            }

            if (process.ChartSeries == null)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] ChartSeries is null, initializing...");
                InitializeChart(process);
            }

            // Aktualizuj zakres osi X
            if (process.ChartXAxes != null && process.ChartXAxes.Length > 0)
            {
                var maxX = memoryValues.Max(v => v.X);
                process.ChartXAxes[0].MaxLimit = Math.Max(maxX + 2, 10); // Co najmniej 10 sekund
            }

            // Aktualizuj zakres osi Y dla lepszej widoczności
            if (process.ChartYAxes != null && process.ChartYAxes.Length > 0 && memoryValues.Length > 0)
            {
                var minY = memoryValues.Min(v => v.Y);
                var maxY = memoryValues.Max(v => v.Y);
                var range = maxY - minY;
                
                // Jeśli zakres jest bardzo mały (np. wartości prawie identyczne), zwiększ go
                if (range < 1.0)
                {
                    var center = (minY + maxY) / 2;
                    process.ChartYAxes[0].MinLimit = Math.Max(0, center - 5);
                    process.ChartYAxes[0].MaxLimit = center + 5;
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Small range detected, using fixed: {process.ChartYAxes[0].MinLimit:F2} - {process.ChartYAxes[0].MaxLimit:F2}");
                }
                else
                {
                    // Normalny zakres z 10% paddingiem
                    var padding = range * 0.1;
                    process.ChartYAxes[0].MinLimit = Math.Max(0, minY - padding);
                    process.ChartYAxes[0].MaxLimit = maxY + padding;
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Normal range: {process.ChartYAxes[0].MinLimit:F2} - {process.ChartYAxes[0].MaxLimit:F2}");
                }
            }

            if (process.ChartSeries != null && process.ChartSeries.Count > 0)
            {
                var series = process.ChartSeries[0] as LineSeries<double>;
                if (series != null)
                {
                    var values = memoryValues.Select(v => v.Y).ToList();
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Updating series with {values.Count} values: [{string.Join(", ", values.Take(5).Select(v => v.ToString("F2")))}...]");
                    
                    // Jeśli Values to ObservableCollection, musimy ją zaktualizować inaczej
                    if (series.Values is ObservableCollection<double> observableValues)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Updating ObservableCollection");
                        observableValues.Clear();
                        foreach (var v in values)
                        {
                            observableValues.Add(v);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Setting new collection");
                        series.Values = new ObservableCollection<double>(values);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] Series updated successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] ERROR: Series[0] is not LineSeries<double>");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessChart] ERROR: ChartSeries is null or empty");
            }
        }

        private void InitializeChart(ProcessInfo process)
        {
            System.Diagnostics.Debug.WriteLine($"[InitializeChart] Initializing chart for process {process.Id}");
            
            process.ChartXAxes = new[]
            {
                new Axis
                {
                    Name = "Time (seconds)",
                    NamePaint = new SolidColorPaint(SKColors.Black),
                    LabelsPaint = new SolidColorPaint(SKColors.Black),
                    TextSize = 11,
                    MinLimit = 0,
                    ForceStepToMin = true,
                    MinStep = 1
                }
            };

            process.ChartYAxes = new[]
            {
                new Axis
                {
                    Name = "Memory (MB)",
                    NamePaint = new SolidColorPaint(SKColors.Red),
                    LabelsPaint = new SolidColorPaint(SKColors.Red),
                    TextSize = 11,
                    Position = LiveChartsCore.Measure.AxisPosition.Start,
                    MinLimit = 0,
                    // Usuń MaxLimit aby wykres automatycznie dopasowywał skalę
                    ForceStepToMin = true,
                    MinStep = 1
                }
            };

            process.ChartSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Name = "Memory Usage",
                    Values = new ObservableCollection<double>(),
                    Fill = new SolidColorPaint(SKColors.Red.WithAlpha(30)),
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 3 },
                    GeometrySize = 10,  // Większe punkty!
                    GeometryFill = new SolidColorPaint(SKColors.Red),
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 3 },
                    LineSmoothness = 0,  // Brak wygładzania dla lepszej widoczności
                    DataPadding = new LiveChartsCore.Drawing.LvcPoint(1, 1)  // Padding dla lepszej widoczności
                }
            };
            
            System.Diagnostics.Debug.WriteLine($"[InitializeChart] Chart initialized - Series count: {process.ChartSeries.Count}");
        }

        private async Task RefreshAsync()
        {
            if (!await _refreshSemaphore.WaitAsync(0))
                return;

            try
            {
                IsRefreshing = true;
                
                // Store current chart data for tracked processes
                var trackedProcessesData = new Dictionary<int, (bool isTracked, DateTime? startedAt, 
                    ObservableCollection<ISeries>? series, Axis[]? xAxes, Axis[]? yAxes)>();
                
                foreach (var p in Processes.Where(p => p.IsTracked))
                {
                    trackedProcessesData[p.Id] = (p.IsTracked, p.TrackingStartedAt, 
                        p.ChartSeries, p.ChartXAxes, p.ChartYAxes);
                }
                
                var selectedId = SelectedProcess?.Id;

                var newProcesses = await _service.GetProcessesAsync();
                var processList = newProcesses.ToList();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    lock (_processesLock)
                    {
                        Processes.Clear();
                        
                        foreach (var p in processList)
                        {
                            // Restore tracked status and chart data
                            if (trackedProcessesData.ContainsKey(p.Id))
                            {
                                var data = trackedProcessesData[p.Id];
                                p.IsTracked = data.isTracked;
                                p.TrackingStartedAt = data.startedAt;
                                p.ChartSeries = data.series;
                                p.ChartXAxes = data.xAxes;
                                p.ChartYAxes = data.yAxes;
                            }
                            
                            Processes.Add(p);
                        }

                        if (selectedId.HasValue)
                        {
                            SelectedProcess = Processes.FirstOrDefault(p => p.Id == selectedId.Value);
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            finally
            {
                IsRefreshing = false;
                _refreshSemaphore.Release();
            }
        }

        private async Task KillAsync(ProcessInfo process)
        {
            try
            {
                if (process.IsTracked)
                {
                    StopTracking(process.Id);
                }
                
                await _service.KillAsync(process.Id);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show($"Failed to kill process: {ex.Message}", 
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            }
        }

        private async Task ChangePriorityAsync(object[] parameters)
        {
            if (parameters == null || parameters.Length < 2)
            {
                await ShowErrorAsync("Invalid parameters for priority change.");
                return;
            }
            
            var process = parameters[0] as ProcessInfo;
            if (process == null)
            {
                await ShowErrorAsync("Process not found.");
                return;
            }
            
            if (!(parameters[1] is System.Diagnostics.ProcessPriorityClass priority))
            {
                await ShowErrorAsync("Priority not specified.");
                return;
            }

            try
            {
                await _service.ChangePriorityAsync(process.Id, priority);
                
                process.Priority = priority;
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show($"Priority changed successfully to {priority}.", 
                        "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                });
                
                await RefreshAsync();
            }
            catch (UnauthorizedAccessException)
            {
                await ShowErrorAsync("Access denied. You may need administrator privileges to change process priority.", "Access Denied");
            }
            catch (InvalidOperationException ex)
            {
                await ShowErrorAsync($"Failed to change priority: {ex.Message}");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Unexpected error: {ex.Message}");
            }
        }

        private async Task ShowErrorAsync(string message, string title = "Error")
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(message, title, 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }

        private void ToggleTracking(ProcessInfo process)
        {
            if (process.IsTracked)
            {
                StopTracking(process.Id);
            }
            else
            {
                StartTracking(process);
            }
        }

        private void StartTracking(ProcessInfo process)
        {
            System.Diagnostics.Debug.WriteLine($"[StartTracking] Starting tracking for process {process.Id} ({process.Name})");
            
            process.IsTracked = true;
            process.TrackingStartedAt = DateTime.Now;

            var monitored = new MonitoredProcess
            {
                ProcessId = process.Id,
                Name = process.Name,
                StartedAt = DateTime.Now
            };

            lock (_processesLock)
            {
                MonitoredProcesses.Add(monitored);
            }
            
            System.Diagnostics.Debug.WriteLine($"[StartTracking] MonitoredProcess created, total monitored: {MonitoredProcesses.Count}");

            var cts = new CancellationTokenSource();
            _trackingTokens[process.Id] = cts;
            _ = _tracking.TrackAsync(monitored, SamplingInterval, cts.Token);
            
            System.Diagnostics.Debug.WriteLine($"[StartTracking] TrackAsync started with interval {SamplingInterval}ms");
            
            InitializeChart(process);
            
            System.Diagnostics.Debug.WriteLine($"[StartTracking] Chart initialized, ChartSeries != null: {process.ChartSeries != null}");
            
            ProcessesView.Refresh();
        }

        private void StopTracking(int processId)
        {
            var process = Processes.FirstOrDefault(p => p.Id == processId);
            if (process != null)
            {
                process.IsTracked = false;
                process.TrackingStartedAt = null;
                process.ChartSeries = null;
                process.ChartXAxes = null;
                process.ChartYAxes = null;
            }

            if (_trackingTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _trackingTokens.Remove(processId);
            }

            var monitored = MonitoredProcesses.FirstOrDefault(m => m.ProcessId == processId);
            if (monitored != null && monitored.FinishedAt == null)
            {
                monitored.FinishedAt = DateTime.Now;
            }
            
            ProcessesView.Refresh();
        }

        private void ShowMonitoredWindow()
        {
            var window = new Views.MonitoredProcessesWindow
            {
                DataContext = this
            };
            window.Show();
        }

        private async Task LoadProcessDetailsAsync(ProcessInfo process)
        {
            try
            {
                var details = await _service.GetProcessDetailsAsync(process.Id);
                if (details != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        process.Threads.Clear();
                        foreach (var thread in details.Threads)
                            process.Threads.Add(thread);
                            
                        process.Modules.Clear();
                        foreach (var module in details.Modules)
                            process.Modules.Add(module);
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch
            {
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
