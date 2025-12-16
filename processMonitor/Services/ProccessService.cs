using processMonitor.Models;
using System.Diagnostics;

namespace processMonitor.Services
{
    public class ProcessService
    {
        private readonly HashSet<string> _systemProcesses = new HashSet<string>
        {
            "System", "Registry", "smss", "csrss", "wininit", "services", 
            "lsass", "svchost", "Memory Compression"
        };

        public async Task<IEnumerable<ProcessInfo>> GetProcessesAsync(bool includeSystemProcesses = true)
        {
            return await Task.Run(() => GetProcesses(includeSystemProcesses));
        }

        public IEnumerable<ProcessInfo> GetProcesses(bool includeSystemProcesses = true)
        {
            return Process.GetProcesses()
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(p =>
                {
                    try
                    {
                        if (!includeSystemProcesses && _systemProcesses.Contains(p.ProcessName))
                        {
                            p.Dispose();
                            return null;
                        }

                        var processInfo = new ProcessInfo
                        {
                            Id = p.Id,
                            Name = p.ProcessName,
                            MemoryMB = p.WorkingSet64 / 1024.0 / 1024.0,
                            ThreadCount = p.Threads.Count,
                            Priority = p.PriorityClass
                        };
                        
                        p.Dispose();
                        return processInfo;
                    }
                    catch
                    {
                        try { p.Dispose(); } catch { }
                        return null;
                    }
                })
                .Where(p => p != null)!
                .OrderBy(p => p.Name);
        }

        public async Task<ProcessInfo?> GetProcessDetailsAsync(int processId)
        {
            return await Task.Run(() => GetProcessDetails(processId));
        }

        public ProcessInfo? GetProcessDetails(int processId)
        {
            try
            {
                using var p = Process.GetProcessById(processId);
                var processInfo = new ProcessInfo
                {
                    Id = p.Id,
                    Name = p.ProcessName,
                    MemoryMB = p.WorkingSet64 / 1024.0 / 1024.0,
                    ThreadCount = p.Threads.Count,
                    Priority = p.PriorityClass
                };
                
                int threadCount = 0;
                foreach (ProcessThread thread in p.Threads)
                {
                    if (threadCount++ >= 50) break;
                    
                    try
                    {
                        processInfo.Threads.Add(new ThreadInfo
                        {
                            Id = thread.Id,
                            State = thread.ThreadState.ToString(),
                            Priority = thread.PriorityLevel.ToString()
                        });
                    }
                    catch { }
                }
                
                try
                {
                    int moduleCount = 0;
                    foreach (ProcessModule module in p.Modules)
                    {
                        if (moduleCount++ >= 100) break;
                        
                        processInfo.Modules.Add(new ModuleInfo
                        {
                            ModuleName = module.ModuleName ?? string.Empty,
                            FileName = module.FileName ?? string.Empty
                        });
                    }
                }
                catch { }
                
                return processInfo;
            }
            catch
            {
                return null;
            }
        }

        public async Task KillAsync(int pid)
        {
            await Task.Run(() => Kill(pid));
        }

        public void Kill(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.Kill();
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to kill process {pid}: {ex.Message}", ex);
            }
        }

        public async Task ChangePriorityAsync(int pid, ProcessPriorityClass priority)
        {
            await Task.Run(() => ChangePriority(pid, priority));
        }

        public void ChangePriority(int pid, ProcessPriorityClass priority)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.PriorityClass = priority;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to change priority for process {pid}: {ex.Message}", ex);
            }
        }
    }
}