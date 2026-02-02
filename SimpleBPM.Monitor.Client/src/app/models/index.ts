// --- Process Definition ---
export interface ProcessDefinitionDto {
  id: string;
  name: string;
  version: string;
  startNodeId: string;
  nodes: ProcessNodeDto[];
  connections: NodeConnectionDto[];
}

export interface ProcessNodeDto {
  id: string;
  name: string;
  type: NodeType;
  isStartNode: boolean;
  nextNodeIds: string[];
  metadata?: Record<string, any>;
}

export interface NodeConnectionDto {
  fromNodeId: string;
  toNodeId: string;
  label?: string;
}

export type NodeType = 'Business' | 'Decision' | 'Interactive' | 'WaitUntilDate' | 'WaitForSignal' | 'SubProcess';

// --- Process Instance ---
export interface ProcessInstanceDto {
  id: string;
  aggregateId?: string;
  definitionName?: string;
  definitionVersion?: string;
  status: ProcessStatus;
  errorMessage?: string;
  variables: Record<string, any>;
  currentNodeId?: string;
  currentNodeName?: string;
  startedAt: string;
  completedAt?: string;
  lastExecutedAt?: string;
  duration?: string;
  completedSteps: number;
  failedSteps: number;
  executionHistory: NodeExecutionHistoryDto[];
  pendingSignals: string[];
  subProcessIds: Record<string, string>;
}

export type ProcessStatus = 'Running' | 'WaitingInteraction' | 'WaitingDate' | 'WaitingSignal' | 'Completed' | 'Failed';

export interface NodeExecutionHistoryDto {
  nodeId: string;
  nodeName: string;
  nodeType: string;
  startedAt: string;
  completedAt: string;
  duration: string;
  success: boolean;
  errorMessage?: string;
  nextNodeId?: string;
}

export interface InstanceListResponse {
  items: ProcessInstanceDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// --- Dashboard ---
export interface DashboardDto {
  totalInstances: number;
  runningCount: number;
  waitingInteractionCount: number;
  waitingSignalCount: number;
  waitingDateCount: number;
  completedCount: number;
  failedCount: number;
  definitionCount: number;
  statusBreakdown: StatusBreakdownItem[];
  definitionSummaries: DefinitionSummaryDto[];
  recentFailed: ProcessInstanceDto[];
  longRunning: ProcessInstanceDto[];
}

export interface StatusBreakdownItem {
  status: string;
  count: number;
  color: string;
}

export interface DefinitionSummaryDto {
  name: string;
  version: string;
  nodeCount: number;
  activeInstances: number;
}

// --- Admin Actions ---
export interface ActionResultDto {
  success: boolean;
  message: string;
  instanceId?: string;
}

export interface AuditLogEntry {
  timestamp: string;
  action: string;
  instanceId: string;
  details?: string;
  user?: string;
}

// --- Requests ---
export interface TerminateRequest {
  reason: string;
}

export interface ForceCompleteNodeRequest {
  nodeId: string;
  outputVariables?: Record<string, any>;
  reason?: string;
}

export interface SendSignalRequest {
  signalName: string;
}

export interface CreateInstanceRequest {
  definitionName: string;
  variables?: Record<string, any>;
}

export interface SetVariablesRequest {
  variables: Record<string, any>;
}
