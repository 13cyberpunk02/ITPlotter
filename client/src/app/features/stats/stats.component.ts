import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatsService } from '../../core/services/stats.service';
import { PrintStatsDto } from '../../core/models/stats.models';

@Component({
  selector: 'app-stats',
  imports: [CommonModule],
  templateUrl: './stats.component.html',
  styleUrl: './stats.component.css',
})
export class StatsComponent implements OnInit {
  private readonly statsService = inject(StatsService);

  stats = signal<PrintStatsDto | null>(null);
  loading = signal(true);
  error = signal(false);

  ngOnInit(): void {
    this.statsService.getMyStats().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  maxRecentPages(): number {
    const recent = this.stats()?.recent;
    if (!recent || recent.length === 0) return 1;
    return Math.max(...recent.map((d) => d.pages), 1);
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
  }
}
