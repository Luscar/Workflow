import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DashboardDto,
  ProcessDefinitionDto,
  ProcessInstanceDto,
  InstanceListResponse,
  ActionResultDto,
  AuditLogEntry,
  TerminateRequest,
  ForceCompleteNodeRequest,
  SendSignalRequest,
  CreateInstanceRequest,
  SetVariablesRequest
} from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = '/api';

  constructor(private http: HttpClient) {}

  // --- Dashboard ---
  getDashboard(): Observable<DashboardDto> {
    return this.http.get<DashboardDto>(`${this.baseUrl}/dashboard`);
  }

  getAuditLog(count: number = 100): Observable<AuditLogEntry[]> {
    return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/dashboard/audit-log`, {
      params: new HttpParams().set('count', count.toString())
    });
  }

  // --- Definitions ---
  getDefinitions(): Observable<ProcessDefinitionDto[]> {
    return this.http.get<ProcessDefinitionDto[]>(`${this.baseUrl}/definitions`);
  }

  getDefinition(name: string, version?: string): Observable<ProcessDefinitionDto> {
    let params = new HttpParams();
    if (version) params = params.set('version', version);
    return this.http.get<ProcessDefinitionDto>(`${this.baseUrl}/definitions/${encodeURIComponent(name)}`, { params });
  }

  // --- Instances ---
  getInstances(page: number = 1, pageSize: number = 50, status?: string, definitionName?: string, search?: string): Observable<InstanceListResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) params = params.set('status', status);
    if (definitionName) params = params.set('definitionName', definitionName);
    if (search) params = params.set('search', search);
    return this.http.get<InstanceListResponse>(`${this.baseUrl}/instances`, { params });
  }

  getInstance(id: string): Observable<ProcessInstanceDto> {
    return this.http.get<ProcessInstanceDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}`);
  }

  getChildInstances(parentId: string): Observable<ProcessInstanceDto[]> {
    return this.http.get<ProcessInstanceDto[]>(`${this.baseUrl}/instances/${encodeURIComponent(parentId)}/children`);
  }

  // --- Admin Actions ---
  createInstance(request: CreateInstanceRequest): Observable<ActionResultDto> {
    return this.http.post<ActionResultDto>(`${this.baseUrl}/instances`, request);
  }

  terminateInstance(id: string, request: TerminateRequest): Observable<ActionResultDto> {
    return this.http.post<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}/terminate`, request);
  }

  forceCompleteNode(id: string, request: ForceCompleteNodeRequest): Observable<ActionResultDto> {
    return this.http.post<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}/force-complete`, request);
  }

  sendSignal(id: string, request: SendSignalRequest): Observable<ActionResultDto> {
    return this.http.post<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}/signal`, request);
  }

  setVariables(id: string, request: SetVariablesRequest): Observable<ActionResultDto> {
    return this.http.put<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}/variables`, request);
  }

  retryInstance(id: string): Observable<ActionResultDto> {
    return this.http.post<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}/retry`, {});
  }

  deleteInstance(id: string): Observable<ActionResultDto> {
    return this.http.delete<ActionResultDto>(`${this.baseUrl}/instances/${encodeURIComponent(id)}`);
  }
}
