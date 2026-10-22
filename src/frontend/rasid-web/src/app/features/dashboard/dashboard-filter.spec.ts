import { DefaultFilter, toApiFilter } from './dashboard-filter';

describe('toApiFilter', () => {
  const now = new Date('2026-10-20T12:00:00Z');

  it('turns a relative period into an explicit UTC range ending now', () => {
    const filter = toApiFilter({ ...DefaultFilter, period: '24h' }, now);
    expect(filter.toUtc).toBe('2026-10-20T12:00:00.000Z');
    expect(filter.fromUtc).toBe('2026-10-19T12:00:00.000Z');
  });

  it('trims the keyword and drops it when empty', () => {
    expect(toApiFilter({ ...DefaultFilter, keyword: '  convoy ' }, now).keyword).toBe('convoy');
    expect(toApiFilter({ ...DefaultFilter, keyword: '   ' }, now).keyword).toBeNull();
  });

  it('falls back to 7 days when a custom range is incomplete', () => {
    const filter = toApiFilter({ ...DefaultFilter, period: 'custom', customFrom: null, customTo: null }, now);
    expect(new Date(filter.toUtc!).getTime() - new Date(filter.fromUtc!).getTime()).toBe(7 * 24 * 3_600_000);
  });

  it('passes engagement type and level through', () => {
    const filter = toApiFilter({ ...DefaultFilter, engagementType: 'Likes', level: 'High' }, now);
    expect(filter.engagementType).toBe('Likes');
    expect(filter.level).toBe('High');
  });
});
