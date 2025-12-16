using System;

namespace processMonitor.Models
{
    public class PerformanceData
    {
        public DateTime Time { get; set; }
        public double MemoryMB { get; set; }
        public double CpuPercent { get; set; }
    }
}
