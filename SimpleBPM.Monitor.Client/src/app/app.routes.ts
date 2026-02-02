import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout';

export const routes: Routes = [
  {
    path: '',
    component: LayoutComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./components/dashboard/dashboard').then(m => m.DashboardComponent)
      },
      {
        path: 'definitions',
        loadComponent: () => import('./components/definitions/definition-list').then(m => m.DefinitionListComponent)
      },
      {
        path: 'definitions/:name',
        loadComponent: () => import('./components/definitions/definition-detail').then(m => m.DefinitionDetailComponent)
      },
      {
        path: 'instances',
        loadComponent: () => import('./components/instances/instance-list').then(m => m.InstanceListComponent)
      },
      {
        path: 'instances/:id',
        loadComponent: () => import('./components/instances/instance-detail').then(m => m.InstanceDetailComponent)
      },
      {
        path: 'audit',
        loadComponent: () => import('./components/shared/audit-log').then(m => m.AuditLogComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
