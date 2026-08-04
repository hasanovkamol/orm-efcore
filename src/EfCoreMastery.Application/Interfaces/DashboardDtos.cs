namespace EfCoreMastery.Application.Interfaces;

public class BenchmarkMetricDto
{
    public int Level { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ApproachA { get; set; } = string.Empty;
    public double TimeA { get; set; } // in ms
    public double MemoryA { get; set; } // in KB
    public string ApproachB { get; set; } = string.Empty;
    public double TimeB { get; set; } // in ms
    public double MemoryB { get; set; } // in KB
    public string Winner { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class LiveBenchmarkResultDto
{
    public string BenchmarkName { get; set; } = string.Empty;
    public double ExecutionTimeMs { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public int RecordCount { get; set; }
}
