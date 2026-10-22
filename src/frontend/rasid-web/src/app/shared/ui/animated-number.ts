import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';

export type NumberFormat = 'compact' | 'integer' | 'rate';

const compact = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });
const DurationMs = 900;

/**
 * Counts up from the previous value to the new one (ease-out). Shows the final value immediately
 * for users who prefer reduced motion, and "—" / "N/A" when there is no value.
 */
@Component({
  selector: 'rasid-animated-number',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `{{ text() }}`,
  host: { '[attr.aria-label]': 'finalText()' },
})
export class AnimatedNumber {
  readonly value = input.required<number | null>();
  readonly format = input<NumberFormat>('compact');

  private readonly shown = signal<number | null>(null);
  private frame = 0;

  protected readonly text = computed(() => this.render(this.shown()));
  protected readonly finalText = computed(() => this.render(this.value()));

  constructor() {
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));

    effect(() => {
      const target = this.value();
      cancelAnimationFrame(this.frame);

      if (target === null || prefersReducedMotion()) {
        this.shown.set(target);
        return;
      }

      const start = this.shown() ?? 0;
      const began = performance.now();
      const step = (now: number) => {
        const progress = Math.min(1, (now - began) / DurationMs);
        const eased = 1 - Math.pow(1 - progress, 3);
        this.shown.set(start + (target - start) * eased);
        if (progress < 1) {
          this.frame = requestAnimationFrame(step);
        }
      };
      this.frame = requestAnimationFrame(step);
    });
  }

  private render(value: number | null): string {
    if (value === null) {
      return this.format() === 'rate' ? 'N/A' : '—';
    }

    switch (this.format()) {
      case 'rate':
        return `${value.toFixed(2)}%`;
      case 'integer':
        return integer.format(Math.round(value));
      default:
        return compact.format(Math.round(value));
    }
  }
}

export function prefersReducedMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}
