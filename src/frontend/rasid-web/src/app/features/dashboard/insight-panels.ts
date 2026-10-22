import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatTooltip } from '@angular/material/tooltip';
import { ActivityHeatmap, Highlight } from '../../core/models/api.models';
import { Tone } from '../../shared/ui/kpi-card';

const HighlightIcons: Record<string, string> = {
  tag: 'tag',
  person: 'person_celebrate',
  schedule: 'schedule',
  rule: 'rule',
  translate: 'translate',
};

/** Plain-language findings computed by the API, shown as a strip of cards. */
@Component({
  selector: 'rasid-insight-highlights',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="strip">
      @for (h of highlights(); track h.title; let i = $index) {
        <article class="card r-reveal r-lift" [style.--i]="i">
          <span class="icon material-symbols-rounded" aria-hidden="true">{{ icon(h.icon) }}</span>
          <div>
            <h3>{{ h.title }}</h3>
            <p>{{ h.detail }}</p>
          </div>
        </article>
      }
    </div>
  `,
  styles: `
    .strip { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 240px), 1fr)); }
    .card {
      display: flex; gap: 12px; align-items: flex-start; padding: 14px 16px;
      border-radius: var(--r-radius); border: 1px solid var(--r-border);
      background: linear-gradient(135deg, var(--r-brand-soft), var(--r-surface) 60%);
    }
    .icon { display: grid; place-items: center; width: 36px; height: 36px; flex: none; border-radius: 10px;
            background: var(--r-surface); color: var(--r-brand); box-shadow: var(--r-shadow);
            font-variation-settings: 'FILL' 1, 'wght' 500, 'GRAD' 0, 'opsz' 24; }
    h3 { margin: 0 0 2px; font-size: 0.9rem; font-weight: 700; line-height: 1.3; }
    p { margin: 0; font-size: 0.8rem; color: var(--r-text-muted); line-height: 1.4; }
  `,
})
export class InsightHighlights {
  readonly highlights = input.required<Highlight[]>();

  protected icon(key: string): string {
    return HighlightIcons[key] ?? 'insights';
  }
}

export interface RankedItem {
  key: string;
  label: string;
  sublabel?: string;
  value: string;
  /** 0–100, drives the bar width. */
  share: number;
  initials?: string;
  imageUrl?: string | null;
}

/** Ranked list with animated share bars (hashtags, accounts, rules, languages). */
@Component({
  selector: 'rasid-ranked-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltip],
  template: `
    <section class="r-card" [class]="'r-card tone-' + tone()">
      <h2 class="r-card__title">
        <span class="title"><span class="material-symbols-rounded" aria-hidden="true">{{ icon() }}</span>{{ title() }}</span>
        @if (caption()) {
          <span class="r-chip">{{ caption() }}</span>
        }
      </h2>
      @if (items().length === 0) {
        <p class="r-muted empty">{{ emptyText() }}</p>
      } @else {
        <ol>
          @for (item of items(); track item.key; let i = $index) {
            <li class="r-reveal" [style.--i]="i">
              @if (item.initials !== undefined) {
                @if (item.imageUrl) {
                  <img class="avatar" [src]="item.imageUrl" alt="" width="32" height="32" loading="lazy" referrerpolicy="no-referrer" />
                } @else {
                  <span class="avatar avatar--initials" aria-hidden="true">{{ item.initials }}</span>
                }
              } @else {
                <span class="rank" aria-hidden="true">{{ i + 1 }}</span>
              }
              <div class="main">
                <div class="row">
                  <span class="label" dir="auto" [matTooltip]="item.label">{{ item.label }}</span>
                  <span class="value">{{ item.value }}</span>
                </div>
                @if (item.sublabel) {
                  <span class="sub">{{ item.sublabel }}</span>
                }
                <div class="r-bar" role="presentation"><span [style.width.%]="item.share" [style.animation-delay.ms]="i * 70"></span></div>
              </div>
            </li>
          }
        </ol>
      }
    </section>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .title { display: flex; align-items: center; gap: 8px; }
    .title .material-symbols-rounded { color: var(--tone); font-size: 20px; }
    ol { margin: 0; padding: 0; list-style: none; display: grid; gap: 14px; }
    li { display: flex; gap: 12px; align-items: center; }
    .rank { width: 26px; height: 26px; flex: none; display: grid; place-items: center; border-radius: 8px;
            background: color-mix(in srgb, var(--tone) 14%, transparent); color: var(--tone); font-weight: 800; font-size: 0.78rem; }
    .avatar { width: 32px; height: 32px; flex: none; border-radius: 50%; object-fit: cover; }
    .avatar--initials { display: grid; place-items: center; background: color-mix(in srgb, var(--tone) 16%, transparent); color: var(--tone); font-weight: 700; font-size: 0.78rem; }
    .main { flex: 1; min-width: 0; display: grid; gap: 4px; }
    .row { display: flex; justify-content: space-between; gap: 8px; }
    .label { font-weight: 600; font-size: 0.88rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .value { font-weight: 700; font-size: 0.82rem; white-space: nowrap; }
    .sub { font-size: 0.74rem; color: var(--r-text-muted); }
    .empty { margin: 0; font-size: 0.85rem; }
  `,
})
export class RankedList {
  readonly title = input.required<string>();
  readonly icon = input('leaderboard');
  readonly tone = input<Tone>('brand');
  readonly caption = input('');
  readonly items = input.required<RankedItem[]>();
  readonly emptyText = input('Nothing to rank for this filter.');
}

const Days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
// Weekday order starting Monday reads naturally for most analysts.
const DayOrder = [1, 2, 3, 4, 5, 6, 0];

/** Posts by weekday and hour (local time). Darker = more posts; the peak cell is outlined. */
@Component({
  selector: 'rasid-activity-heatmap',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltip],
  template: `
    <div class="wrap">
      <div class="grid" role="table" aria-label="Posts by weekday and hour">
        <span class="corner" aria-hidden="true"></span>
        @for (h of hours; track h) {
          <span class="hour" aria-hidden="true">{{ h % 3 === 0 ? (h < 10 ? '0' + h : h) : '' }}</span>
        }
        @for (d of dayOrder; track d) {
          <span class="day">{{ days[d] }}</span>
          @for (h of hours; track h) {
            <span
              class="cell"
              role="cell"
              [class.cell--peak]="d === heatmap().peakDay && h === heatmap().peakHour && heatmap().max > 0"
              [style.--level]="level(d, h)"
              [matTooltip]="days[d] + ' ' + (h < 10 ? '0' + h : h) + ':00 · ' + heatmap().cells[d][h] + ' posts'"
              [attr.aria-label]="days[d] + ' ' + h + ':00, ' + heatmap().cells[d][h] + ' posts'"
            ></span>
          }
        }
      </div>
    </div>
    <div class="legend" aria-hidden="true">
      <span>Fewer</span>
      @for (step of [0.08, 0.3, 0.55, 0.8, 1]; track step) {
        <span class="cell" [style.--level]="step"></span>
      }
      <span>More</span>
      @if (heatmap().max > 0) {
        <span class="peak">Peak: {{ days[heatmap().peakDay] }} {{ peakHourLabel() }}</span>
      }
    </div>
  `,
  styles: `
    .wrap { overflow-x: auto; scrollbar-width: thin; }
    .grid { display: grid; grid-template-columns: 36px repeat(24, minmax(14px, 1fr)); gap: 3px; min-width: 520px; }
    .hour, .day { font-size: 0.66rem; color: var(--r-text-subtle); display: grid; place-items: center; }
    .day { justify-content: start; font-weight: 600; }
    .cell {
      aspect-ratio: 1; border-radius: 4px;
      background: color-mix(in srgb, var(--r-brand) calc(var(--level, 0) * 100%), var(--r-surface-2));
      transition: transform 0.15s ease;
    }
    .cell:hover { transform: scale(1.25); }
    .cell--peak { outline: 2px solid var(--r-accent); outline-offset: 1px; }
    .legend { display: flex; align-items: center; flex-wrap: wrap; gap: 4px; margin-top: 12px; font-size: 0.72rem; color: var(--r-text-muted); }
    .legend .cell { width: 14px; }
    .peak { margin-left: auto; font-weight: 700; color: var(--r-text); }
  `,
})
export class ActivityHeatmapChart {
  readonly heatmap = input.required<ActivityHeatmap>();

  protected readonly days = Days;
  protected readonly dayOrder = DayOrder;
  protected readonly hours = Array.from({ length: 24 }, (_, i) => i);

  protected readonly peakHourLabel = computed(() => {
    const h = this.heatmap().peakHour;
    return `${h < 10 ? '0' + h : h}:00`;
  });

  protected level(day: number, hour: number): number {
    const max = this.heatmap().max;
    const value = this.heatmap().cells[day]?.[hour] ?? 0;
    return max === 0 || value === 0 ? 0 : 0.12 + (value / max) * 0.88;
  }
}
