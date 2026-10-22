import { CompactNumberPipe, RatePipe, RelativeTimePipe } from './format.pipes';

describe('format pipes', () => {
  it('shows N/A for a missing engagement rate instead of an estimate', () => {
    const pipe = new RatePipe();
    expect(pipe.transform(null)).toBe('N/A');
    expect(pipe.transform(2.756)).toBe('2.76%');
  });

  it('shows an em dash for missing numbers', () => {
    expect(new CompactNumberPipe().transform(null)).toBe('—');
  });

  it('formats relative times and handles a missing value', () => {
    const pipe = new RelativeTimePipe();
    const now = Date.UTC(2026, 9, 20, 12, 0, 0);
    expect(pipe.transform(null, now)).toBe('never');
    expect(pipe.transform(new Date(now - 5 * 60_000).toISOString(), now)).toMatch(/5/);
  });
});
