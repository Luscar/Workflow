import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ProcessDefinitionDto } from '../../models';

@Component({
  selector: 'app-definition-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './definition-list.html',
  styleUrl: './definition-list.scss'
})
export class DefinitionListComponent implements OnInit {
  definitions: ProcessDefinitionDto[] = [];
  loading = true;
  error: string | null = null;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getDefinitions().subscribe({
      next: data => {
        this.definitions = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load definitions';
        this.loading = false;
      }
    });
  }

  getNodeTypeColor(type: string): string {
    const colors: Record<string, string> = {
      'Business': '#3b82f6',
      'Decision': '#f59e0b',
      'Interactive': '#22c55e',
      'WaitUntilDate': '#06b6d4',
      'WaitForSignal': '#8b5cf6',
      'SubProcess': '#ec4899'
    };
    return colors[type] || '#6b7280';
  }

  getNodeTypeCounts(def: ProcessDefinitionDto): { type: string; count: number; color: string }[] {
    const counts = new Map<string, number>();
    def.nodes.forEach(n => {
      counts.set(n.type, (counts.get(n.type) || 0) + 1);
    });
    return Array.from(counts.entries()).map(([type, count]) => ({
      type,
      count,
      color: this.getNodeTypeColor(type)
    }));
  }
}
