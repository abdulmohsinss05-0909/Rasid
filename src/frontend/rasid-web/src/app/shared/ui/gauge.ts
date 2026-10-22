import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EngagementLevel } from '../../core/models/api.models';
import { AnimatedNumber } from './animated-number';
import { LevelDisplay } from './status-display';

/**
 * Semicircular gauge for the engagement rate. The scale tops out at twice the "high" threshold,
 * and the arc takes the colour of the engagement level.
 */
@Component({
  selector: 'rasid-gauge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AnimatedNumber],
  template: `
    <div class="gauge" role="img" [attr.aria-label]="'Engagement rate ' + (rate() ?? 'not available') + ', level ' + levelLabel()">
      <svg viewBox="0 0 200 116" aria-hidden="true">
        <path class="track" d="M16 104 A84 84 0 0 1 184 104" pathLength="100" />
        <path class="value" d="M16 104 A84 84 0 0 1 184 104" pathLength="100"
              [style.stroke]="color()" [style.--fill]="fill()" />
        @for (tick of ticks(); track tick.label) {
          <circle [attr.cx]="tick.x" [attr.cy]="tick.y" r="2.5" class="tick" />
        }
      </svg>
      <div class="readout">
        <strong><rasid-animated-number [value]="rate()" format="rate" /></strong>
        <span [style.color]="color()">{{ levelLabel() }}</span>
      </div>
    </div>
  `,
  styles: `
    .gauge { position: relative; width: min(240px, 100%); margin: 0 auto; }
    svg { display: block; width: 100%; }
    path { fill: none; stroke-width: 16; stroke-linecap: round; }
    .track { stroke: var(--r-surface-3); }
    .value { stroke-dasharray: 100; stroke-dashoffset: calc(100 - var(--fill)); transition: stroke-dashoffset 1s cubic-bezier(.2,.8,.2,1);
             animation: sweep 1.1s cubic-bezier(.2,.8,.2,1); }
    @keyframes sweep { from { stroke-dashoffset: 100; } }
    .tick { fill: var(--r-surface); stroke: var(--r-text-subtle); stroke-width: 1.5; }
    .readout { position: absolute; inset: auto 0 2px 0; display: grid; justify-items: center; line-height: 1.1; }
    .readout strong { font-size: 1.9rem; font-weight: 800; letter-spacing: -0.03em; }
    .readout span { font-weight: 700; font-size: 0.85rem; }
    @media (prefers-reduced-motion: reduce) { .value { animation: none; transition: none; } }
  `,
})
export class Gauge {
  readonly rate = input.required<number | null>();
  readonly level = input.required<EngagementLevel>();
  readonly mediumFrom = input(1);
  readonly highAbove = input(3);

  protected readonly scaleMax = computed(() => this.highAbove() * 2);
  protected readonly fill = computed(() => Math.max(0, Math.min(100, ((this.rate() ?? 0) / this.scaleMax()) * 100)));
  protected readonly color = computed(() => LevelDisplay[this.level()].color);
  protected readonly levelLabel = computed(() => (this.level() === 'Unknown' ? 'No view data' : `${LevelDisplay[this.level()].label} engagement`));

  /** Markers at the medium and high thresholds on the arc. */
  protected readonly ticks = computed(() =>
    [this.mediumFrom(), this.highAbove()].map((threshold) => {
      const angle = Math.PI * (1 - threshold / this.scaleMax());
      return { label: String(threshold), x: 100 + 84 * Math.cos(angle), y: 104 - 84 * Math.sin(angle) };
    }));
}
