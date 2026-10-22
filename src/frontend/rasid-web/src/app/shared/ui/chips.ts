import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DataSource, EngagementLevel, RefreshStatus } from '../../core/models/api.models';
import { LevelDisplay, RefreshStatusDisplay } from './status-display';

/** Collection status of a topic. */
@Component({
  selector: 'rasid-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="r-chip" [class]="'r-chip--' + display().tone" [attr.title]="display().label">
      <span class="material-symbols-rounded" aria-hidden="true">{{ display().icon }}</span>
      {{ display().label }}
    </span>
  `,
})
export class StatusChip {
  readonly status = input.required<RefreshStatus>();
  protected readonly display = computed(() => RefreshStatusDisplay[this.status()]);
}

/** Engagement level badge (High / Medium / Low / N/A). */
@Component({
  selector: 'rasid-level-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="r-chip level" [style.--level-color]="display().color">
      <span class="r-dot" aria-hidden="true"></span>{{ display().label }}
    </span>
  `,
  styles: `
    .level {
      color: var(--level-color);
      background: color-mix(in srgb, var(--level-color) 14%, transparent);
    }
  `,
})
export class LevelChip {
  readonly level = input.required<EngagementLevel>();
  protected readonly display = computed(() => LevelDisplay[this.level()]);
}

/** Always-visible data-source label: live X data or clearly marked sample data. */
@Component({
  selector: 'rasid-source-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (source()) {
      @case ('XApi') {
        <span class="r-chip r-chip--success" title="Official X API v2">
          <span class="r-dot r-dot--pulse" aria-hidden="true"></span> Live · X API v2
        </span>
      }
      @case ('Sample') {
        <span class="r-chip r-chip--warning" title="Fictitious demonstration data — not real posts">
          <span class="material-symbols-rounded" aria-hidden="true">science</span> SAMPLE DATA
        </span>
      }
      @default {
        <span class="r-chip r-chip--danger">
          <span class="material-symbols-rounded" aria-hidden="true">link_off</span> No data source
        </span>
      }
    }
  `,
})
export class SourceBadge {
  readonly source = input.required<DataSource | null>();
}
