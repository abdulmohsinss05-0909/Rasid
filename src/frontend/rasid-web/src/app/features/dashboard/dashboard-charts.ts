import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { ChartConfiguration, ChartType, ScriptableContext } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { EngagementBreakdown, LevelDistribution, SeriesGranularity, SeriesPoint } from '../../core/models/api.models';
import { ThemeService } from '../../core/theme/theme.service';
import { chartPalette } from '../../shared/charts/chart-palette';

const hourFormat = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
const dayFormat = new Intl.DateTimeFormat(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
const numberFormat = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });

type SeriesKey = 'posts' | 'likes' | 'reposts' | 'replies' | 'quotes';

interface SeriesDefinition {
  key: SeriesKey;
  label: string;
  light: string;
  dark: string;
}

const SeriesDefinitions: SeriesDefinition[] = [
  { key: 'posts', label: 'Posts', light: '#0f8f86', dark: '#2dd4bf' },
  { key: 'likes', label: 'Likes', light: '#e11d48', dark: '#fb7185' },
  { key: 'reposts', label: 'Reposts', light: '#059669', dark: '#34d399' },
  { key: 'replies', label: 'Replies', light: '#2563eb', dark: '#60a5fa' },
  { key: 'quotes', label: 'Quotes', light: '#7c3aed', dark: '#a78bfa' },
];

/**
 * Engagement over time: posts (left axis) and each interaction type (right axis) as smooth area
 * lines. Series can be toggled; unusually busy periods (spikes) are marked on the posts line.
 */
@Component({
  selector: 'rasid-engagement-trend-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BaseChartDirective],
  template: `
    <div class="toggles" role="group" aria-label="Series">
      @for (s of definitions; track s.key) {
        <button
          type="button"
          class="toggle"
          [class.toggle--on]="visible().has(s.key)"
          [style.--c]="theme.isDark() ? s.dark : s.light"
          [attr.aria-pressed]="visible().has(s.key)"
          (click)="toggle(s.key)"
        >
          <span class="dot"></span>{{ s.label }}
        </button>
      }
      @if (spikeCount() > 0) {
        <span class="spikes" title="Periods with unusually high posting activity">
          <span class="material-symbols-rounded" aria-hidden="true">bolt</span>{{ spikeCount() }} spike{{ spikeCount() === 1 ? '' : 's' }}
        </span>
      }
    </div>
    <div class="chart" role="img" [attr.aria-label]="'Posts and interactions per ' + granularity().toLowerCase()">
      <canvas baseChart [type]="chartType" [data]="data()" [options]="options()"></canvas>
    </div>
  `,
  styles: `
    .toggles { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; align-items: center; }
    .toggle {
      display: inline-flex; align-items: center; gap: 6px; height: 30px; padding: 0 12px; border-radius: 999px;
      border: 1px solid var(--r-border); background: transparent; color: var(--r-text-muted);
      font: 600 0.78rem var(--r-font); cursor: pointer; transition: all 0.15s ease;
    }
    .toggle .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--c); opacity: 0.4; }
    .toggle--on { color: var(--r-text); border-color: color-mix(in srgb, var(--c) 50%, var(--r-border)); background: color-mix(in srgb, var(--c) 10%, transparent); }
    .toggle--on .dot { opacity: 1; }
    .spikes { margin-left: auto; display: inline-flex; align-items: center; gap: 2px; font-size: 0.78rem; font-weight: 700; color: var(--r-accent); }
    .spikes .material-symbols-rounded { font-size: 18px; }
    .chart { position: relative; height: 320px; }
  `,
})
export class EngagementTrendChart {
  readonly series = input.required<SeriesPoint[]>();
  readonly granularity = input.required<SeriesGranularity>();

  protected readonly theme = inject(ThemeService);
  protected readonly definitions = SeriesDefinitions;
  protected readonly chartType: ChartType = 'line';
  protected readonly visible = signal(new Set<SeriesKey>(['posts', 'likes', 'reposts']));
  protected readonly spikeCount = computed(() => this.series().filter((p) => p.isSpike).length);

  protected toggle(key: SeriesKey): void {
    this.visible.update((current) => {
      const next = new Set(current);
      if (next.has(key) && next.size > 1) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  protected readonly data = computed<ChartConfiguration<ChartType>['data']>(() => {
    const dark = this.theme.isDark();
    const accent = chartPalette(dark).accent;
    const format = this.granularity() === 'Hour' ? hourFormat : dayFormat;
    const points = this.series();
    const dense = points.length > 48;

    return {
      labels: points.map((p) => format.format(new Date(p.bucketStartUtc))),
      datasets: SeriesDefinitions.filter((s) => this.visible().has(s.key)).map((s) => {
        const color = dark ? s.dark : s.light;
        const isPosts = s.key === 'posts';
        return {
          type: 'line' as const,
          label: s.label,
          data: points.map((p) => p[s.key]),
          yAxisID: isPosts ? 'posts' : 'value',
          borderColor: color,
          borderWidth: isPosts ? 3 : 2.25,
          tension: 0.38,
          fill: isPosts ? 'origin' : false,
          backgroundColor: (ctx: ScriptableContext<'line'>) => areaGradient(ctx, color),
          pointRadius: isPosts ? points.map((p) => (p.isSpike ? 7 : dense ? 0 : 3)) : dense ? 0 : 2.5,
          pointStyle: isPosts ? points.map((p) => (p.isSpike ? 'triangle' : 'circle')) : 'circle',
          pointBackgroundColor: isPosts ? points.map((p) => (p.isSpike ? accent : color)) : color,
          pointBorderColor: isPosts ? points.map((p) => (p.isSpike ? accent : color)) : color,
          pointHoverRadius: 6,
        };
      }),
    };
  });

  protected readonly options = computed<ChartConfiguration<ChartType>['options']>(() => {
    const palette = chartPalette(this.theme.isDark());
    const showValueAxis = [...this.visible()].some((k) => k !== 'posts');
    return {
      responsive: true,
      maintainAspectRatio: false,
      animation: { duration: 900, easing: 'easeOutQuart' },
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: { display: false },
        tooltip: {
          padding: 12,
          cornerRadius: 12,
          boxPadding: 4,
          usePointStyle: true,
          callbacks: {
            afterTitle: (items) => (this.series()[items[0]?.dataIndex ?? -1]?.isSpike ? '⚡ Activity spike' : ''),
          },
        },
      },
      scales: {
        x: { grid: { display: false }, ticks: { color: palette.muted, maxRotation: 0, autoSkipPadding: 18 } },
        posts: {
          position: 'left',
          beginAtZero: true,
          grid: { color: palette.grid },
          border: { display: false },
          ticks: { color: palette.muted, precision: 0 },
          title: { display: true, text: 'Posts', color: palette.muted },
        },
        value: {
          display: showValueAxis,
          position: 'right',
          beginAtZero: true,
          grid: { display: false },
          border: { display: false },
          ticks: { color: palette.muted, callback: (v) => numberFormat.format(Number(v)) },
          title: { display: true, text: 'Interactions', color: palette.muted },
        },
      },
    };
  });
}

/** Vertical gradient from the series colour to transparent, sized to the chart area. */
function areaGradient(ctx: ScriptableContext<'line'>, color: string): CanvasGradient | string {
  const { chart } = ctx;
  const area = chart.chartArea;
  if (!area) {
    return 'transparent';
  }
  const gradient = chart.ctx.createLinearGradient(0, area.top, 0, area.bottom);
  gradient.addColorStop(0, `${color}55`);
  gradient.addColorStop(1, `${color}00`);
  return gradient;
}

/** Share of likes, reposts, replies and quotes, with the total in the centre. */
@Component({
  selector: 'rasid-breakdown-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BaseChartDirective],
  template: `
    <div class="chart" role="img" aria-label="Engagement mix by interaction type">
      <canvas baseChart type="doughnut" [data]="data()" [options]="options()"></canvas>
      <div class="centre" aria-hidden="true">
        <strong>{{ total() }}</strong>
        <span>interactions</span>
      </div>
    </div>
  `,
  styles: `
    .chart { position: relative; height: 260px; }
    .centre { position: absolute; inset: 0 0 44px 0; display: grid; place-content: center; text-align: center; pointer-events: none; }
    .centre strong { font-size: 1.5rem; font-weight: 800; letter-spacing: -0.02em; }
    .centre span { font-size: 0.75rem; color: var(--r-text-muted); }
  `,
})
export class BreakdownChart {
  readonly breakdown = input.required<EngagementBreakdown>();
  private readonly theme = inject(ThemeService);

  protected readonly total = computed(() => {
    const b = this.breakdown();
    return numberFormat.format(b.likes + b.reposts + b.replies + b.quotes);
  });

  protected readonly data = computed<ChartConfiguration<'doughnut'>['data']>(() => {
    const dark = this.theme.isDark();
    const palette = chartPalette(dark);
    const b = this.breakdown();
    return {
      labels: ['Likes', 'Reposts', 'Replies', 'Quotes'],
      datasets: [
        {
          data: [b.likes, b.reposts, b.replies, b.quotes],
          backgroundColor: SeriesDefinitions.slice(1).map((s) => (dark ? s.dark : s.light)),
          borderColor: palette.surface,
          borderWidth: 3,
          borderRadius: 6,
          hoverOffset: 8,
        },
      ],
    };
  });

  protected readonly options = computed<ChartConfiguration<'doughnut'>['options']>(() => {
    const palette = chartPalette(this.theme.isDark());
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '72%',
      animation: { animateRotate: true, duration: 1000 },
      plugins: {
        legend: { position: 'bottom', labels: { color: palette.muted, usePointStyle: true, boxWidth: 8, padding: 14 } },
        tooltip: {
          callbacks: {
            label: (ctx) => {
              const total = (ctx.dataset.data as number[]).reduce((sum, v) => sum + v, 0) || 1;
              const value = Number(ctx.raw);
              return ` ${ctx.label}: ${numberFormat.format(value)} (${((value / total) * 100).toFixed(1)}%)`;
            },
          },
        },
      },
    };
  });
}

/** How many posts fall into each engagement level. */
@Component({
  selector: 'rasid-levels-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BaseChartDirective],
  template: `
    <div class="chart" role="img" aria-label="Posts by engagement level">
      <canvas baseChart type="bar" [data]="data()" [options]="options()"></canvas>
    </div>
  `,
  styles: `.chart { position: relative; height: 200px; }`,
})
export class LevelsChart {
  readonly levels = input.required<LevelDistribution>();
  private readonly theme = inject(ThemeService);

  protected readonly data = computed<ChartConfiguration<'bar'>['data']>(() => {
    const palette = chartPalette(this.theme.isDark());
    const l = this.levels();
    return {
      labels: ['High', 'Medium', 'Low', 'N/A'],
      datasets: [{ label: 'Posts', data: [l.high, l.medium, l.low, l.unknown], backgroundColor: palette.levels, borderRadius: 8, barThickness: 20 }],
    };
  });

  protected readonly options = computed<ChartConfiguration<'bar'>['options']>(() => {
    const palette = chartPalette(this.theme.isDark());
    return {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      animation: { duration: 900 },
      plugins: { legend: { display: false } },
      scales: {
        x: { beginAtZero: true, grid: { color: palette.grid }, border: { display: false }, ticks: { color: palette.muted, precision: 0 } },
        y: { grid: { display: false }, border: { display: false }, ticks: { color: palette.text, font: { weight: 600 } } },
      },
    };
  });
}
