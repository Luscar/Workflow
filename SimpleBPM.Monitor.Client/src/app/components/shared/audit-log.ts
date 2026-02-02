import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuditLogEntry } from '../../models';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.scss'
})
export class AuditLogComponent implements OnInit {
  entries: AuditLogEntry[] = [];
  loading = true;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadAuditLog();
  }

  loadAuditLog() {
    this.loading = true;
    this.api.getAuditLog(200).subscribe({
      next: data => {
        this.entries = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleString();
  }

  truncateId(id: string): string {
    return id.length > 12 ? id.substring(0, 12) + '...' : id;
  }

  getActionClass(action: string): string {
    switch (action) {
      case 'Terminate': case 'Delete': return 'action-danger';
      case 'ForceCompleteNode': return 'action-warning';
      case 'SendSignal': case 'CreateInstance': return 'action-success';
      default: return 'action-info';
    }
  }
}
