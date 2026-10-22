import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatTooltip } from '@angular/material/tooltip';
import { AnimatedNumber, NumberFormat } from './animated-number';
import { Sparkline } from './sparkline';
import { TrendChip } from './trend-chip';

export type Tone = 'brand' | 'rose' | 'green' | 'violet' | 'amber' | 'blue';

/**
 * Headline metric: coloured icon, animated value, period-over-period trend and a sparkline.
 * Content projected into the card (e.g. a level chip) appears under the value.
 */
@Component({
  selector: 'rasid-kpi-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltip, AnimatedNumber, TrendChip, Sparkline],
  template: `
    <article class="kpi r-lift" [class]="'kpi r-lift tone-' + tone()" [class.kpi--highlight]="highlight()">
      <header class="kpi__head">
        <span class="kpi__icon" aria-hidden="true"><span class="material-symbols-rounded">{{ icon() }}</span></span>
        <span class="kpi__label">{{ label() }}</span>
        @if (info()) {
          <span class="kpi__info material-symbols-rounded" tabindex="0" [matTooltip]="info()" [attr.aria-label]="info()">info</span>
        }
      </header>

      <div class="kpi__value"><rasid-animated-number [value]="value()" [format]="format()" /></div>

      <div class="kpi__meta">
        @if (showTrend()) {
          <rasid-trend [value]="trend()" [unit]="trendUnit()" [period]="period()" />
        }
        @if (hint()) {
          <span class="kpi__hint">{{ hint() }}</span>
        }
      </div>
      <ng-content />

      @if (spark().length > 1) {
        <rasid-sparkline class="kpi__spark" [values]="spark()" [color]="highlight() ? '#ffffff' : 'var(--tone)'" [height]="34" />
      }
    </article>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .kpi {
      position: relative; height: 100%; overflow: hidden;
      display: grid; gap: 8px; align-content: start;
      padding: 16px 16px 12px;
      border-radius: var(--r-radius);
      background: var(--r-surface);
      border: 1px solid var(--r-border);
      box-shadow: var(--r-shadow);
    }
    .kpi::after {
      content: ''; position: absolute; inset: 0 0 auto auto; width: 120px; height: 120px; border-radius: 50%;
      background: radial-gradient(circle, color-mix(in srgb, var(--tone) 16%, transparent), transparent 70%);
      transform: translate(40%, -40%); pointer-events: none;
    }
    .kpi--highlight { background: var(--r-gradient); border-color: transparent; color: #fff; --r-text-muted: rgb(255 255 255 / 82%); }
    .kpi--highlight .kpi__icon { background: rgb(255 255 255 / 18%); color: #fff; }
    .kpi__head { display: flex; align-items: center; gap: 10px; font-size: 0.82rem; font-weight: 600; color: var(--r-text-muted); }
    .kpi__icon { display: grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; flex: none;
                 background: color-mix(in srgb, var(--tone) 14%, transparent); color: var(--tone); }
    .kpi__icon .material-symbols-rounded { font-size: 20px; font-variation-settings: 'FILL' 1, 'wght' 500, 'GRAD' 0, 'opsz' 24; }
    .kpi__label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .kpi__info { font-size: 18px; cursor: help; }
    .kpi__value { font-size: clamp(1.45rem, 2.6vw, 1.9rem); font-weight: 800; letter-spacing: -0.03em; line-height: 1.1; }
    .kpi__meta { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 8px; min-height: 22px; }
    .kpi__hint { font-size: 0.74rem; color: var(--r-text-muted); }
    .kpi__spark { height: 34px; margin: 2px -4px 0; opacity: 0.95; }
  `,
})
export class KpiCard {
  readonly label = input.required<string>();
  readonly value = input.required<number | null>();
  readonly format = input<NumberFormat>('compact');
  readonly icon = input('insights');
  readonly tone = input<Tone>('brand');
  readonly trend = input<number | null>(null);
  readonly trendUnit = input<'%' | 'pts'>('%');
  readonly showTrend = input(true);
  readonly period = input('previous period');
  readonly spark = input<readonly number[]>([]);
  readonly hint = input<string | null>(null);
  readonly info = input('');
  readonly highlight = input(false);
}
