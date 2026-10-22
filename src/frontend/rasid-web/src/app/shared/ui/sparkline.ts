import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

let nextId = 0;

/** Tiny trend line (pure SVG, no chart library) that draws itself in on first render. */
@Component({
  selector: 'rasid-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 ' + width + ' ' + height()" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient [attr.id]="gradientId" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" [attr.stop-color]="color()" stop-opacity="0.32" />
          <stop offset="1" [attr.stop-color]="color()" stop-opacity="0" />
        </linearGradient>
      </defs>
      @if (path(); as p) {
        <path class="area" [attr.d]="p.area" [attr.fill]="'url(#' + gradientId + ')'" />
        <path class="line" [attr.d]="p.line" [attr.stroke]="color()" pathLength="1" />
      }
    </svg>
  `,
  styles: `
    :host { display: block; width: 100%; }
    svg { display: block; width: 100%; height: 100%; overflow: visible; }
    .line { fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; vector-effect: non-scaling-stroke;
            stroke-dasharray: 1; stroke-dashoffset: 1; animation: draw 1.1s ease-out forwards; }
    .area { opacity: 0; animation: fade 0.8s 0.4s ease-out forwards; }
    @keyframes draw { to { stroke-dashoffset: 0; } }
    @keyframes fade { to { opacity: 1; } }
    @media (prefers-reduced-motion: reduce) { .line { animation: none; stroke-dashoffset: 0; } .area { animation: none; opacity: 1; } }
  `,
})
export class Sparkline {
  readonly values = input.required<readonly number[]>();
  readonly color = input('var(--r-brand)');
  readonly height = input(40);

  protected readonly width = 120;
  protected readonly gradientId = `rasid-spark-${nextId++}`;

  protected readonly path = computed(() => {
    const values = this.values();
    if (values.length < 2) {
      return null;
    }

    const h = this.height();
    const max = Math.max(...values, 1);
    const stepX = this.width / (values.length - 1);
    const points = values.map((v, i) => [i * stepX, h - 2 - (v / max) * (h - 6)] as const);
    const line = points.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
    return { line, area: `${line} L${this.width},${h} L0,${h} Z` };
  });
}
