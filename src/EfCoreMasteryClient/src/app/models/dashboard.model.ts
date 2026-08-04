export interface BenchmarkMetric {
  level: number;
  levelName: string;
  operation: string;
  approachA: string;
  timeA: number;
  memoryA: number;
  approachB: string;
  timeB: number;
  memoryB: number;
  winner: string;
  notes: string;
}

export interface LiveBenchmarkResult {
  benchmarkName: string;
  executionTimeMs: number;
  memoryAllocatedBytes: number;
  recordCount: number;
}
