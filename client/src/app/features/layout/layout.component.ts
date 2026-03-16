import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ThemeToggleComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css',
})
export class LayoutComponent {
  protected readonly auth = inject(AuthService);

  readonly navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/printers', label: 'Printers', icon: 'printer' },
    { path: '/documents', label: 'Documents', icon: 'document' },
    { path: '/print-jobs', label: 'Print Jobs', icon: 'jobs' },
  ];

  constructor() {
    this.auth.loadProfile().subscribe();
  }
}
