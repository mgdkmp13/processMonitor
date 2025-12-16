using System.Diagnostics;
using processMonitor.Models;

namespace processMonitor.Services
{
    public class ProcessMonitorService
    {
        public async Task TrackAsync(
            MonitoredProcess tracked,
            int samplingIntervalMs,
            CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var p = Process.GetProcessById(tracked.ProcessId);
                    
                    var memoryMB = p.WorkingSet64 / 1024.0 / 1024.0;
                    tracked.MemorySamples.Add((DateTime.Now, memoryMB));
                    
                    if (tracked.MemorySamples.Count > 1000)
                    {
                        tracked.MemorySamples.RemoveAt(0);
                    }
                }
                catch (ArgumentException)
                {
                    tracked.FinishedAt = DateTime.Now;
                    break;
                }
                catch (InvalidOperationException)
                {
                    tracked.FinishedAt = DateTime.Now;
                    break;
                }
                catch
                {
                }

                try
                {
                    await Task.Delay(samplingIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            
            if (tracked.FinishedAt == null)
            {
                tracked.FinishedAt = DateTime.Now;
            }
        }
    }
}
