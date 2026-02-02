import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ProcessDefinitionDto } from '../../models';
import { FlowViewerComponent } from '../flow-viewer/flow-viewer';

@Component({
  selector: 'app-definition-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FlowViewerComponent],
  templateUrl: './definition-detail.html',
  styleUrl: './definition-detail.scss'
})
export class DefinitionDetailComponent implements OnInit {
  definition: ProcessDefinitionDto | null = null;
  loading = true;
  error: string | null = null;

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    const name = this.route.snapshot.paramMap.get('name')!;
    const version = this.route.snapshot.queryParamMap.get('version') || undefined;

    this.api.getDefinition(name, version).subscribe({
      next: def => {
        this.definition = def;
        this.loading = false;
      },
      error: () => {
        this.error = `Definition '${name}' not found`;
        this.loading = false;
      }
    });
  }

  getNodeTypeColor(type: string): string {
    const colors: Record<string, string> = {
      'Business': '#3b82f6', 'Decision': '#f59e0b', 'Interactive': '#22c55e',
      'WaitUntilDate': '#06b6d4', 'WaitForSignal': '#8b5cf6', 'SubProcess': '#ec4899'
    };
    return colors[type] || '#6b7280';
  }

  getStartNodeName(): string {
    if (!this.definition) return '';
    const startNode = this.definition.nodes.find(n => n.isStartNode);
    return startNode?.name || this.definition.startNodeId;
  }

  formatMetadata(metadata: Record<string, any> | undefined): string {
    if (!metadata) return '-';
    return Object.entries(metadata)
      .map(([key, value]) => `${key}: ${typeof value === 'object' ? JSON.stringify(value) : value}`)
      .join(', ');
  }
}
