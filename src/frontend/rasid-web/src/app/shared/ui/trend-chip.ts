import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Period-over-period change: ▲ +12% (up) / ▼ −8% (down) / no change.
 * `unit="pts"` is used for rates, where the change is in percentage points.
 */
@Component({
  selector: 'rasid-trend',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (value() === null) {
      <span class="trend trend--none" [attr.title]="'No data for the ' + period()">new</span>
    } @else {
      <span class="trend" [class]="'trend trend--' + direction()" [attr.title]="'vs ' + period()" [attr.aria-label]="label()">
        <span class="material-symbols-rounded" aria-hidden="true">{{ icon() }}</span>{{ formatted() }}
      </span>
    }
  `,
  styles: `
    .trend {
      display: inline-flex; align-items: center; gap: 2px;
      padding: 2px 8px 2px 4px; border-radius: 999px;
      font-size: 0.74rem; font-weight: 700; white-space: nowrap;
    }
    .trend .material-symbols-rounded { font-size: 16px; }
    .trend--up { color: var(--r-success); background: var(--r-success-soft); }
    .trend--down { color: var(--r-danger); background: var(--r-danger-soft); }
    .trend--flat, .trend--none { color: var(--r-text-muted); background: var(--r-surface-2); padding-left: 8px; }
  `,
})
export class TrendChip {
  readonly value = input.required<number | null>();
  readonly unit = input<'%' | 'pts'>('%');
  readonly period = input('previous period');

  protected readonly direction = computed(() => {
    const v = this.value() ?? 0;
    return v > 0 ? 'up' : v < 0 ? 'down' : 'flat';
  });

  protected readonly icon = computed(() =>
    this.direction() === 'up' ? 'arrow_upward' : this.direction() === 'down' ? 'arrow_downward' : 'remove');

  protected readonly formatted = computed(() => {
    const v = this.value() ?? 0;
    const sign = v > 0 ? '+' : v < 0 ? '−' : '';
    const magnitude = Math.abs(v);
    const number = magnitude >= 100 ? magnitude.toFixed(0) : magnitude.toFixed(1);
    return `${sign}${number}${this.unit() === '%' ? '%' : ' pts'}`;
  });

  protected readonly label = computed(() => `${this.formatted()} versus the ${this.period()}`);
}
