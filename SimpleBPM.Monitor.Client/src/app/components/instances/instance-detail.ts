import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';
import { ProcessInstanceDto, ProcessDefinitionDto } from '../../models';
import { FlowViewerComponent } from '../flow-viewer/flow-viewer';

@Component({
  selector: 'app-instance-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, FlowViewerComponent],
  templateUrl: './instance-detail.html',
  styleUrl: './instance-detail.scss'
})
export class InstanceDetailComponent implements OnInit {
  instance: ProcessInstanceDto | null = null;
  definition: ProcessDefinitionDto | null = null;
  children: ProcessInstanceDto[] = [];
  loading = true;
  error: string | null = null;
  actionLoading = false;

  // Modal state
  showTerminateModal = false;
  showForceCompleteModal = false;
  showSignalModal = false;
  showVariablesModal = false;
  showDeleteConfirm = false;

  // Form data
  terminateReason = '';
  forceCompleteReason = '';
  forceCompleteVars = '';
  signalName = '';
  variablesJson = '';

  // Tabs
  activeTab: 'history' | 'variables' | 'subprocesses' = 'history';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private notify: NotificationService
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadInstance(id);
  }

  loadInstance(id: string) {
    this.loading = true;
    this.api.getInstance(id).subscribe({
      next: inst => {
        this.instance = inst;
        this.loading = false;
        this.variablesJson = JSON.stringify(inst.variables, null, 2);

        // Load definition for flow viewer
        if (inst.definitionName) {
          this.api.getDefinition(inst.definitionName, inst.definitionVersion || undefined).subscribe({
            next: def => this.definition = def,
            error: () => {} // Definition might not be available
          });
        }

        // Load children
        if (Object.keys(inst.subProcessIds).length > 0) {
          this.api.getChildInstances(id).subscribe({
            next: children => this.children = children,
            error: () => {}
          });
        }
      },
      error: () => {
        this.error = `Instance '${id}' not found`;
        this.loading = false;
      }
    });
  }

  refresh() {
    if (this.instance) this.loadInstance(this.instance.id);
  }

  // --- Status helpers ---
  getStatusBadgeClass(status: string): string {
    return 'badge badge-' + status.toLowerCase().replace(/\s+/g, '');
  }

  isTerminal(): boolean {
    return this.instance?.status === 'Completed' || this.instance?.status === 'Failed';
  }

  canSendSignal(): boolean {
    return this.instance?.status === 'WaitingSignal';
  }

  canRetry(): boolean {
    return this.instance?.status === 'Failed';
  }

  formatDate(date: string | undefined): string {
    if (!date) return '-';
    return new Date(date).toLocaleString();
  }

  truncateId(id: string): string {
    return id.length > 12 ? id.substring(0, 12) + '...' : id;
  }

  getExecutedNodeIds(): string[] {
    return this.instance?.executionHistory.map(h => h.nodeId) || [];
  }

  getFailedNodeId(): string | null {
    const failed = this.instance?.executionHistory.find(h => !h.success);
    return failed?.nodeId || null;
  }

  // --- Admin Actions ---

  terminate() {
    if (!this.instance) return;
    this.actionLoading = true;
    this.api.terminateInstance(this.instance.id, { reason: this.terminateReason }).subscribe({
      next: result => {
        this.actionLoading = false;
        this.showTerminateModal = false;
        this.terminateReason = '';
        if (result.success) {
          this.notify.success('Instance terminated');
          this.refresh();
        } else {
          this.notify.error(result.message);
        }
      },
      error: err => {
        this.actionLoading = false;
        this.notify.error('Failed to terminate instance');
      }
    });
  }

  forceComplete() {
    if (!this.instance) return;
    this.actionLoading = true;

    let outputVars: Record<string, any> | undefined;
    if (this.forceCompleteVars.trim()) {
      try {
        outputVars = JSON.parse(this.forceCompleteVars);
      } catch {
        this.notify.error('Invalid JSON for output variables');
        this.actionLoading = false;
        return;
      }
    }

    this.api.forceCompleteNode(this.instance.id, {
      nodeId: this.instance.currentNodeId || '',
      outputVariables: outputVars,
      reason: this.forceCompleteReason
    }).subscribe({
      next: result => {
        this.actionLoading = false;
        this.showForceCompleteModal = false;
        this.forceCompleteReason = '';
        this.forceCompleteVars = '';
        if (result.success) {
          this.notify.success('Node force-completed');
          this.refresh();
        } else {
          this.notify.error(result.message);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.notify.error('Failed to force complete node');
      }
    });
  }

  sendSignal() {
    if (!this.instance) return;
    this.actionLoading = true;
    this.api.sendSignal(this.instance.id, { signalName: this.signalName }).subscribe({
      next: result => {
        this.actionLoading = false;
        this.showSignalModal = false;
        this.signalName = '';
        if (result.success) {
          this.notify.success('Signal sent');
          this.refresh();
        } else {
          this.notify.error(result.message);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.notify.error('Failed to send signal');
      }
    });
  }

  saveVariables() {
    if (!this.instance) return;
    this.actionLoading = true;

    let vars: Record<string, any>;
    try {
      vars = JSON.parse(this.variablesJson);
    } catch {
      this.notify.error('Invalid JSON');
      this.actionLoading = false;
      return;
    }

    this.api.setVariables(this.instance.id, { variables: vars }).subscribe({
      next: result => {
        this.actionLoading = false;
        this.showVariablesModal = false;
        if (result.success) {
          this.notify.success('Variables updated');
          this.refresh();
        } else {
          this.notify.error(result.message);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.notify.error('Failed to update variables');
      }
    });
  }

  retry() {
    if (!this.instance) return;
    this.actionLoading = true;
    this.api.retryInstance(this.instance.id).subscribe({
      next: result => {
        this.actionLoading = false;
        if (result.success) {
          this.notify.success('Instance retried');
          this.refresh();
        } else {
          this.notify.error(result.message);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.notify.error('Failed to retry instance');
      }
    });
  }

  deleteInstance() {
    if (!this.instance) return;
    this.actionLoading = true;
    this.api.deleteInstance(this.instance.id).subscribe({
      next: result => {
        this.actionLoading = false;
        this.showDeleteConfirm = false;
        if (result.success) {
          this.notify.success('Instance deleted');
          this.router.navigate(['/instances']);
        } else {
          this.notify.error(result.message);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.notify.error('Failed to delete instance');
      }
    });
  }

  objectKeys(obj: Record<string, any>): string[] {
    return Object.keys(obj || {});
  }

  getChildDefinitionName(childId: string): string {
    const child = this.children.find(c => c.id === childId);
    return child?.definitionName || '-';
  }

  getChildStatus(childId: string): string | null {
    const child = this.children.find(c => c.id === childId);
    return child?.status || null;
  }
}
