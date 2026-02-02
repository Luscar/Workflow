import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { DashboardDto, ProcessInstanceDto } from '../../models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnInit, OnDestroy {
  dashboard: DashboardDto | null = null;
  loading = true;
  error: string | null = null;
  private refreshInterval: any;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadDashboard();
    this.refreshInterval = setInterval(() => this.loadDashboard(), 15000);
  }

  ngOnDestroy() {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  loadDashboard() {
    this.api.getDashboard().subscribe({
      next: data => {
        this.dashboard = data;
        this.loading = false;
      },
      error: err => {
        this.error = 'Failed to load dashboard data';
        this.loading = false;
      }
    });
  }

  getStatusBadgeClass(status: string): string {
    return 'badge badge-' + status.toLowerCase().replace(/\s+/g, '');
  }

  getActiveCount(): number {
    if (!this.dashboard) return 0;
    return this.dashboard.runningCount +
           this.dashboard.waitingInteractionCount +
           this.dashboard.waitingSignalCount +
           this.dashboard.waitingDateCount;
  }

  getChartSegments(): { offset: number; length: number; color: string }[] {
    if (!this.dashboard || this.dashboard.totalInstances === 0) return [];
    const total = this.dashboard.totalInstances;
    const circumference = 2 * Math.PI * 54;
    let offset = 0;
    return this.dashboard.statusBreakdown
      .filter(s => s.count > 0)
      .map(s => {
        const length = (s.count / total) * circumference;
        const segment = { offset, length, color: s.color };
        offset += length;
        return segment;
      });
  }

  truncateId(id: string): string {
    return id.length > 12 ? id.substring(0, 12) + '...' : id;
  }
}
