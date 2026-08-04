import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BenchmarkMetric, LiveBenchmarkResult } from '../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);
  // Use relative URL so it works seamlessly with Nginx reverse proxy and ASP.NET static files
  private baseUrl = '/api/dashboard';

  // Signals for state management
  matrixData = signal<BenchmarkMetric[]>([]);
  liveResults = signal<LiveBenchmarkResult[]>([]);
  loading = signal<boolean>(false);
  liveLoading = signal<boolean>(false);

  fetchMatrix(): void {
    this.loading.set(true);
    this.http.get<BenchmarkMetric[]>(`${this.baseUrl}/matrix`).subscribe({
      next: (data) => {
        this.matrixData.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch matrix', err);
        this.loading.set(false);
      }
    });
  }

  runLiveBenchmark(count: number = 2000): void {
    this.liveLoading.set(true);
    this.http.post<LiveBenchmarkResult[]>(`${this.baseUrl}/run-live?count=${count}`, {}).subscribe({
      next: (data) => {
        this.liveResults.set(data);
        this.liveLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to run live benchmark', err);
        this.liveLoading.set(false);
      }
    });
  }
}
