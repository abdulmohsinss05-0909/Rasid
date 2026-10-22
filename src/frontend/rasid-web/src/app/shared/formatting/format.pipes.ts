import { Pipe, PipeTransform } from '@angular/core';

const compactFormatter = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });
const integerFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });
const relativeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
const dateTimeFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

/** 12,345 → "12.3K". Null/undefined → "—". */
@Pipe({ name: 'compact' })
export class CompactNumberPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : compactFormatter.format(value);
  }
}

/** Full number with grouping, e.g. 12,345. */
@Pipe({ name: 'integer' })
export class IntegerPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : integerFormatter.format(value);
  }
}

/** Engagement rate: 2.75 → "2.75%"; null → "N/A" (never estimated). */
@Pipe({ name: 'rate' })
export class RatePipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? 'N/A' : `${value.toFixed(2)}%`;
  }
}

/** ISO timestamp → "5 minutes ago" in the viewer's language. */
@Pipe({ name: 'relativeTime' })
export class RelativeTimePipe implements PipeTransform {
  transform(value: string | null | undefined, now: number = Date.now()): string {
    if (!value) {
      return 'never';
    }

    const seconds = Math.round((new Date(value).getTime() - now) / 1000);
    const units: [Intl.RelativeTimeFormatUnit, number][] = [
      ['day', 86_400],
      ['hour', 3_600],
      ['minute', 60],
    ];

    for (const [unit, size] of units) {
      if (Math.abs(seconds) >= size) {
        return relativeFormatter.format(Math.round(seconds / size), unit);
      }
    }
    return relativeFormatter.format(seconds, 'second');
  }
}

/** ISO timestamp → local date and time, e.g. "20 Oct 2026, 21:15". */
@Pipe({ name: 'localDateTime' })
export class LocalDateTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? dateTimeFormatter.format(new Date(value)) : '—';
  }
}

export const FormatPipes = [CompactNumberPipe, IntegerPipe, RatePipe, RelativeTimePipe, LocalDateTimePipe] as const;
