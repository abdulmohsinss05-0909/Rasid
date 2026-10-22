import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription, catchError, filter, forkJoin, interval, of, startWith, switchMap } from 'rxjs';
import { Alert, Overview } from '../models/api.models';
import { OverviewService } from './overview.service';

const PollMs = 60_000;
const SeenKey = 'rasid.alerts.seen';

/**
 * Shared, periodically refreshed view of the whole system: the cross-topic overview (also used for
 * the sidebar freshness card) and current alerts (the notification bell). Polling pauses while the
 * tab is hidden. Which alerts were seen is a per-device convenience kept in localStorage.
 */
@Injectable({ providedIn: 'root' })
export class LiveStatusStore {
  private readonly service = inject(OverviewService);
  private polling: Subscription | null = null;

  readonly overview = signal<Overview | null>(null);
  readonly alerts = signal<Alert[]>([]);
  readonly loaded = signal(false);
  private readonly seen = signal<ReadonlySet<string>>(readSeen());

  readonly unseenCount = computed(() => this.alerts().filter((a) => !this.seen().has(a.id)).length);

  start(): void {
    if (this.polling) {
      return;
    }

    this.polling = interval(PollMs)
      .pipe(
        startWith(0),
        filter(() => document.visibilityState === 'visible'),
        switchMap(() => this.load()),
      )
      .subscribe();
  }

  stop(): void {
    this.polling?.unsubscribe();
    this.polling = null;
  }

  /** Immediate reload (e.g. when the overview page opens). */
  refresh(): void {
    this.load().subscribe();
  }

  isSeen(alert: Alert): boolean {
    return this.seen().has(alert.id);
  }

  markAllSeen(): void {
    // Only current alert IDs are kept, so the stored set never grows without bound.
    const ids = new Set(this.alerts().map((a) => a.id));
    this.seen.set(ids);
    writeSeen(ids);
  }

  private load() {
    return forkJoin({
      overview: this.service.getOverview().pipe(catchError(() => of(null))),
      alerts: this.service.getAlerts().pipe(catchError(() => of(null))),
    }).pipe(
      switchMap(({ overview, alerts }) => {
        if (overview) {
          this.overview.set(overview);
        }
        if (alerts) {
          this.alerts.set(alerts);
        }
        this.loaded.set(true);
        return of(undefined);
      }),
    );
  }
}

function readSeen(): ReadonlySet<string> {
  try {
    return new Set(JSON.parse(localStorage.getItem(SeenKey) ?? '[]') as string[]);
  } catch {
    return new Set();
  }
}

function writeSeen(ids: ReadonlySet<string>): void {
  try {
    localStorage.setItem(SeenKey, JSON.stringify([...ids]));
  } catch {
    // Storage unavailable: alerts simply show as unseen again next visit.
  }
}
