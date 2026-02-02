import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { ProcessInstanceDto, InstanceListResponse } from '../../models';

@Component({
  selector: 'app-instance-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './instance-list.html',
  styleUrl: './instance-list.scss'
})
export class InstanceListComponent implements OnInit, OnDestroy {
  response: InstanceListResponse | null = null;
  loading = true;
  error: string | null = null;

  // Filters
  searchTerm = '';
  statusFilter = '';
  definitionFilter = '';
  page = 1;
  pageSize = 25;

  private refreshInterval: any;

  constructor(private api: ApiService, private route: ActivatedRoute) {}

  ngOnInit() {
    // Read initial filters from query params
    const params = this.route.snapshot.queryParamMap;
    this.statusFilter = params.get('status') || '';
    this.definitionFilter = params.get('definition') || '';

    this.loadInstances();
    this.refreshInterval = setInterval(() => this.loadInstances(), 10000);
  }

  ngOnDestroy() {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  loadInstances() {
    this.api.getInstances(
      this.page, this.pageSize,
      this.statusFilter || undefined,
      this.definitionFilter || undefined,
      this.searchTerm || undefined
    ).subscribe({
      next: data => {
        this.response = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load instances';
        this.loading = false;
      }
    });
  }

  onSearch() {
    this.page = 1;
    this.loadInstances();
  }

  onFilterChange() {
    this.page = 1;
    this.loadInstances();
  }

  nextPage() {
    if (this.response && this.page * this.pageSize < this.response.totalCount) {
      this.page++;
      this.loadInstances();
    }
  }

  prevPage() {
    if (this.page > 1) {
      this.page--;
      this.loadInstances();
    }
  }

  get totalPages(): number {
    if (!this.response) return 0;
    return Math.ceil(this.response.totalCount / this.pageSize);
  }

  getStatusBadgeClass(status: string): string {
    return 'badge badge-' + status.toLowerCase().replace(/\s+/g, '');
  }

  truncateId(id: string): string {
    return id.length > 12 ? id.substring(0, 12) + '...' : id;
  }

  formatDate(date: string | undefined): string {
    if (!date) return '-';
    return new Date(date).toLocaleString();
  }

  statuses = ['', 'Running', 'WaitingInteraction', 'WaitingSignal', 'WaitingDate', 'Completed', 'Failed'];
}
