import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface Notification {
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
  id: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private counter = 0;
  private notificationsSubject = new Subject<Notification>();
  notifications$ = this.notificationsSubject.asObservable();

  success(message: string) { this.emit('success', message); }
  error(message: string) { this.emit('error', message); }
  warning(message: string) { this.emit('warning', message); }
  info(message: string) { this.emit('info', message); }

  private emit(type: Notification['type'], message: string) {
    this.notificationsSubject.next({ type, message, id: ++this.counter });
  }
}
