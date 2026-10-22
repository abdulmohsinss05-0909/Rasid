import { DashboardFilter, EngagementLevel, EngagementType } from '../../core/models/api.models';

export type Period = '24h' | '3d' | '7d' | '30d' | 'custom';

/** What the analyst selected in the filter bar (UI state). */
export interface FilterState {
  period: Period;
  customFrom: Date | null;
  customTo: Date | null;
  keyword: string;
  engagementType: EngagementType;
  level: EngagementLevel | null;
}

export const DefaultFilter: FilterState = {
  period: '7d',
  customFrom: null,
  customTo: null,
  keyword: '',
  engagementType: 'All',
  level: null,
};

export const PeriodOptions: { value: Period; label: string }[] = [
  { value: '24h', label: '24 hours' },
  { value: '3d', label: '3 days' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' },
  { value: 'custom', label: 'Custom' },
];

export const EngagementTypeOptions: { value: EngagementType; label: string; icon: string }[] = [
  { value: 'All', label: 'All', icon: 'stacks' },
  { value: 'Likes', label: 'Likes', icon: 'favorite' },
  { value: 'Reposts', label: 'Reposts', icon: 'repeat' },
  { value: 'Replies', label: 'Replies', icon: 'chat_bubble' },
  { value: 'Quotes', label: 'Quotes', icon: 'format_quote' },
];

export const LevelOptions: { value: EngagementLevel | null; label: string }[] = [
  { value: null, label: 'Any level' },
  { value: 'High', label: 'High' },
  { value: 'Medium', label: 'Medium' },
  { value: 'Low', label: 'Low' },
  { value: 'Unknown', label: 'N/A (no views)' },
];

const PeriodHours: Record<Exclude<Period, 'custom'>, number> = { '24h': 24, '3d': 72, '7d': 168, '30d': 720 };

/** Converts UI state into the API filter. Relative periods end "now". */
export function toApiFilter(state: FilterState, now = new Date()): DashboardFilter {
  let from: Date | null;
  let to: Date | null;

  if (state.period === 'custom' && state.customFrom && state.customTo) {
    from = startOfDay(state.customFrom);
    to = endOfDay(state.customTo);
  } else {
    const hours = PeriodHours[state.period === 'custom' ? '7d' : state.period];
    to = now;
    from = new Date(now.getTime() - hours * 3_600_000);
  }

  return {
    fromUtc: from.toISOString(),
    toUtc: to.toISOString(),
    keyword: state.keyword.trim() || null,
    engagementType: state.engagementType,
    level: state.level,
  };
}

export function engagementTypeLabel(type: EngagementType): string {
  return type === 'All' ? 'Interactions' : type;
}

function startOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d;
}

function endOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(23, 59, 59, 999);
  return d;
}
