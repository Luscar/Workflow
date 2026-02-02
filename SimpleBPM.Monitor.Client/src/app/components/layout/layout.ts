import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NotificationService, Notification } from '../../services/notification.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  templateUrl: './layout.html',
  styleUrl: './layout.scss'
})
export class LayoutComponent {
  notifications: Notification[] = [];

  constructor(private notificationService: NotificationService) {
    this.notificationService.notifications$.subscribe(n => {
      this.notifications.push(n);
      setTimeout(() => {
        this.notifications = this.notifications.filter(x => x.id !== n.id);
      }, 4000);
    });
  }

  dismissNotification(id: number) {
    this.notifications = this.notifications.filter(n => n.id !== id);
  }
}
