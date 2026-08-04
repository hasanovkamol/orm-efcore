import { Component, ElementRef, ViewChild, computed, effect, inject, signal, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from './services/dashboard.service';
import { BenchmarkMetric } from './models/dashboard.model';
import Chart from 'chart.js/auto';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class AppComponent implements AfterViewInit {
  dashboardService = inject(DashboardService);

  @ViewChild('matrixChart') matrixCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('liveChart') liveCanvas!: ElementRef<HTMLCanvasElement>;

  selectedCategory = signal<string>('all');
  
  // Computed Signal for filtering Level 1-9 matrix data
  filteredMatrix = computed(() => {
    const data = this.dashboardService.matrixData();
    const cat = this.selectedCategory();
    if (cat === 'Junior') return data.filter(d => d.level <= 3);
    if (cat === 'Middle') return data.filter(d => d.level >= 4 && d.level <= 6);
    if (cat === 'Senior') return data.filter(d => d.level >= 7);
    return data;
  });

  private matrixChartInstance?: Chart;
  private liveChartInstance?: Chart;

  constructor() {
    // Effect to update matrix chart when data changes
    effect(() => {
      const data = this.dashboardService.matrixData();
      if (data.length > 0 && this.matrixCanvas) {
        this.renderMatrixChart(data);
      }
    });

    // Effect to update live chart when results arrive
    effect(() => {
      const live = this.dashboardService.liveResults();
      if (live.length > 0 && this.liveCanvas) {
        this.renderLiveChart(live);
      }
    });
  }

  ngAfterViewInit(): void {
    this.dashboardService.fetchMatrix();
    this.dashboardService.runLiveBenchmark();
  }

  setFilter(category: string): void {
    this.selectedCategory.set(category);
  }

  triggerLiveTest(): void {
    this.dashboardService.runLiveBenchmark();
  }

  private renderMatrixChart(data: BenchmarkMetric[]): void {
    if (this.matrixChartInstance) this.matrixChartInstance.destroy();

    const ctx = this.matrixCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.matrixChartInstance = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: data.map(d => `L${d.level} ${d.operation.split('(')[0]}`),
        datasets: [
          {
            label: 'Standard Approach (ms)',
            data: data.map(d => d.timeA),
            backgroundColor: 'rgba(239, 68, 68, 0.75)',
            borderRadius: 6
          },
          {
            label: 'Optimized Approach (ms)',
            data: data.map(d => d.timeB),
            backgroundColor: 'rgba(16, 185, 129, 0.75)',
            borderRadius: 6
          }
        ]
      },
      options: {
        responsive: true,
        scales: {
          y: {
            type: 'logarithmic',
            ticks: { color: '#94a3b8' },
            grid: { color: 'rgba(255, 255, 255, 0.05)' }
          },
          x: {
            ticks: { color: '#94a3b8', font: { size: 10 } },
            grid: { display: false }
          }
        },
        plugins: {
          legend: { labels: { color: '#f8fafc' } }
        }
      }
    });
  }

  private renderLiveChart(live: any[]): void {
    if (this.liveChartInstance) this.liveChartInstance.destroy();

    const ctx = this.liveCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.liveChartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels: live.map(r => r.benchmarkName),
        datasets: [{
          label: 'Live Execution Time (ms) [2000 Records]',
          data: live.map(r => r.executionTimeMs),
          borderColor: '#818cf8',
          backgroundColor: 'rgba(129, 140, 248, 0.25)',
          fill: true,
          tension: 0.4,
          pointRadius: 6,
          pointBackgroundColor: '#c084fc'
        }]
      },
      options: {
        responsive: true,
        scales: {
          y: {
            ticks: { color: '#94a3b8' },
            grid: { color: 'rgba(255, 255, 255, 0.05)' }
          },
          x: {
            ticks: { color: '#94a3b8', font: { size: 11 } },
            grid: { display: false }
          }
        },
        plugins: {
          legend: { labels: { color: '#f8fafc' } }
        }
      }
    });
  }
}
