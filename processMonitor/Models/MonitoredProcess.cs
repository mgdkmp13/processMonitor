namespace processMonitor.Models
{
    public class MonitoredProcess
    {
        public int ProcessId { get; set; }
        public string Name { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public List<(DateTime time, double memory)> MemorySamples { get; }
            = new();

        public TimeSpan TrackingDuration =>
            (FinishedAt ?? DateTime.Now) - StartedAt;

        public double AverageMemory =>
            MemorySamples.Any() ? MemorySamples.Average(s => s.memory) : 0;
    }
}
