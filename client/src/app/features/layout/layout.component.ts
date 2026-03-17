import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  adminOnly?: boolean;
}

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ThemeToggleComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css',
})
export class LayoutComponent {
  protected readonly auth = inject(AuthService);

  private readonly allNavItems: NavItem[] = [
    { path: '/', label: 'Печать', icon: 'print' },
    { path: '/stats', label: 'Статистика', icon: 'stats' },
    { path: '/admin/printers', label: 'Принтеры', icon: 'printer', adminOnly: true },
    { path: '/admin/print-jobs', label: 'Очередь', icon: 'jobs', adminOnly: true },
  ];

  navItems = computed(() => {
    const isAdmin = this.auth.user()?.role === 'Admin';
    return this.allNavItems.filter((item) => !item.adminOnly || isAdmin);
  });

  constructor() {
    this.auth.loadProfile().subscribe();
  }
}
