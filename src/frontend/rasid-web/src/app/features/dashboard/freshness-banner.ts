import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { DataFreshness } from '../../core/models/api.models';
import { LocalDateTimePipe, RelativeTimePipe } from '../../shared/formatting/format.pipes';
import { SourceBadge, StatusChip } from '../../shared/ui/chips';
import { RefreshStatusDisplay } from '../../shared/ui/status-display';

/** Data source, last update and collection status — always visible above the numbers. */
@Component({
  selector: 'rasid-freshness-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButton, SourceBadge, StatusChip, RelativeTimePipe, LocalDateTimePipe],
  template: `
    @let f = freshness();
    <section class="banner" [class]="'banner banner--' + tone()" aria-live="polite">
      <div class="banner__chips">
        <rasid-source-badge [source]="f.source" />
        <rasid-status-chip [status]="f.status" />
        @if (f.isStale) {
          <span class="r-chip r-chip--warning"><span class="material-symbols-rounded">history</span> Stale</span>
        }
      </div>

      <div class="banner__text">
        <p class="banner__message">{{ f.message }}</p>
        <p class="banner__meta">
          Last updated
          <strong [title]="f.lastUpdatedUtc | localDateTime">{{ f.lastUpdatedUtc | relativeTime }}</strong>
          @if (f.lastUpdatedUtc) {
            &nbsp;<span class="r-subtle">({{ f.lastUpdatedUtc | localDateTime }})</span>
          }
          @if (f.retryAfterUtc && (f.status === 'RateLimited' || f.status === 'Deferred')) {
            · next attempt after <strong>{{ f.retryAfterUtc | localDateTime }}</strong>
          }
          · Source: <strong>{{ f.sourceLabel }}</strong>
        </p>
        @if (f.isSampleData) {
          <p class="banner__sample">
            <span class="material-symbols-rounded">science</span>
            Posts and accounts shown are fictitious demonstration data, not real X content.
          </p>
        }
      </div>

      @if (canRefresh()) {
        <button matButton="tonal" class="banner__action" type="button" (click)="refresh.emit()" [disabled]="refreshing()">
          <span class="material-symbols-rounded btn-icon">refresh</span> {{ refreshing() ? 'Queued…' : 'Refresh now' }}
        </button>
      }
    </section>
  `,
  styles: `
    .banner {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 12px;
      padding: 16px 18px;
      border-radius: var(--r-radius);
      border: 1px solid var(--r-border);
      background: var(--r-surface);
      border-left: 4px solid var(--tone);
      --tone: var(--r-success);
      @media (min-width: 900px) { grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; }
    }
    .banner--warning { --tone: var(--r-warning); }
    .banner--danger { --tone: var(--r-danger); }
    .banner--info { --tone: var(--r-info); }
    .banner--neutral { --tone: var(--r-level-low); }
    .banner__chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .banner__text { display: grid; gap: 4px; min-width: 0; }
    .banner__message { margin: 0; font-weight: 600; }
    .banner__meta { margin: 0; font-size: 0.82rem; color: var(--r-text-muted); }
    .banner__sample { margin: 2px 0 0; display: flex; align-items: center; gap: 6px; font-size: 0.8rem; color: var(--r-warning); }
    .banner__sample .material-symbols-rounded { font-size: 16px; }
    .btn-icon { font-size: 18px; }
  `,
})
export class FreshnessBanner {
  readonly freshness = input.required<DataFreshness>();
  readonly canRefresh = input(false);
  readonly refreshing = input(false);
  readonly refresh = output();

  protected readonly tone = computed(() => {
    const f = this.freshness();
    if (f.isStale && f.status === 'Success') {
      return 'warning';
    }
    const tone = RefreshStatusDisplay[f.status].tone;
    return tone === 'brand' ? 'success' : tone;
  });
}
