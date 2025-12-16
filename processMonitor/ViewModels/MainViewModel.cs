using processMonitor.Commands;
using processMonitor.Models;
using processMonitor.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

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
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(RefreshInterval));
            
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await RefreshAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RefreshAsync()
        {
            if (!await _refreshSemaphore.WaitAsync(0))
                return;

            try
            {
                IsRefreshing = true;
                
                var trackedIds = Processes.Where(p => p.IsTracked).Select(p => p.Id).ToHashSet();
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
                            if (trackedIds.Contains(p.Id))
                            {
                                p.IsTracked = true;
                                var tracked = MonitoredProcesses.FirstOrDefault(m => m.ProcessId == p.Id);
                                p.TrackingStartedAt = tracked?.StartedAt;
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

            var cts = new CancellationTokenSource();
            _trackingTokens[process.Id] = cts;
            _ = _tracking.TrackAsync(monitored, SamplingInterval, cts.Token);
            
            ProcessesView.Refresh();
        }

        private void StopTracking(int processId)
        {
            var process = Processes.FirstOrDefault(p => p.Id == processId);
            if (process != null)
            {
                process.IsTracked = false;
                process.TrackingStartedAt = null;
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
