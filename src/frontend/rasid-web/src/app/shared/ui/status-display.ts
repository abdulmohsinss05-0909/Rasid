import { AlertKind, EngagementLevel, RefreshStatus } from '../../core/models/api.models';

export type Tone = 'success' | 'warning' | 'danger' | 'info' | 'brand' | 'neutral';

export interface StatusDisplay {
  label: string;
  icon: string;
  tone: Tone;
}

/** How each collection status is presented. One table so every screen shows the same wording. */
export const RefreshStatusDisplay: Record<RefreshStatus, StatusDisplay> = {
  Pending: { label: 'Collecting', icon: 'hourglass_top', tone: 'info' },
  Success: { label: 'Up to date', icon: 'check_circle', tone: 'success' },
  NoResults: { label: 'No matches', icon: 'search_off', tone: 'neutral' },
  Deferred: { label: 'Deferred (quota)', icon: 'schedule', tone: 'warning' },
  RateLimited: { label: 'Rate limited', icon: 'speed', tone: 'warning' },
  AuthFailed: { label: 'Credentials error', icon: 'key_off', tone: 'danger' },
  ConnectionFailed: { label: 'Connection failed', icon: 'cloud_off', tone: 'danger' },
  NotConfigured: { label: 'Not configured', icon: 'settings_alert', tone: 'danger' },
};

export const LevelDisplay: Record<EngagementLevel, { label: string; color: string }> = {
  High: { label: 'High', color: 'var(--r-level-high)' },
  Medium: { label: 'Medium', color: 'var(--r-level-medium)' },
  Low: { label: 'Low', color: 'var(--r-level-low)' },
  Unknown: { label: 'N/A', color: 'var(--r-level-unknown)' },
};

export const AlertKindIcon: Record<AlertKind, string> = {
  ActivitySpike: 'bolt',
  CollectionProblem: 'sync_problem',
  StaleData: 'history',
  Quota: 'account_balance_wallet',
};
