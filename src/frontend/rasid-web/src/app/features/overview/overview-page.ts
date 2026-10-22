import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Alert } from '../../core/models/api.models';
import { LiveStatusStore } from '../../core/services/live-status.store';
import { LocalDateTimePipe, RelativeTimePipe } from '../../shared/formatting/format.pipes';
import { AnimatedNumber } from '../../shared/ui/animated-number';
import { LevelChip, SourceBadge, StatusChip } from '../../shared/ui/chips';
import { KpiCard } from '../../shared/ui/kpi-card';
import { Sparkline } from '../../shared/ui/sparkline';
import { AlertKindIcon } from '../../shared/ui/status-display';
import { TrendChip } from '../../shared/ui/trend-chip';

/** Home screen: every topic at a glance, with 7-day trends, sparklines and current alerts. */
@Component({
  selector: 'rasid-overview-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, MatButton, MatProgressBar, KpiCard, TrendChip, Sparkline, AnimatedNumber,
    StatusChip, LevelChip, SourceBadge, RelativeTimePipe, LocalDateTimePipe,
  ],
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage implements OnInit {
  protected readonly store = inject(LiveStatusStore);
  protected readonly auth = inject(AuthService);

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
    const name = this.auth.user()?.displayName.split(' ')[0] ?? '';
    return name ? `${part}, ${name}` : part;
  });

  /** Daily totals across all topics, for the KPI sparklines. */
  protected readonly totalsByDay = computed(() => {
    const topics = this.store.overview()?.topics ?? [];
    const days = this.store.overview()?.days.length ?? 0;
    const sum = (pick: (t: (typeof topics)[number]) => number[]) =>
      Array.from({ length: days }, (_, i) => topics.reduce((total, t) => total + (pick(t)[i] ?? 0), 0));
    return { posts: sum((t) => t.postsByDay), interactions: sum((t) => t.interactionsByDay) };
  });

  ngOnInit(): void {
    this.store.refresh();
  }

  protected alertIcon(alert: Alert): string {
    return AlertKindIcon[alert.kind];
  }
}
