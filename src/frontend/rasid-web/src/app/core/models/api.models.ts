/**
 * Contracts of the Rasid API (/api/v1). They mirror the backend DTOs one-to-one;
 * enums travel as strings.
 */

export type TopicPriority = 'Low' | 'Normal' | 'High';
export type RuleType = 'Keyword' | 'Hashtag' | 'Account';
export type DataSource = 'XApi' | 'Sample';
export type EngagementType = 'All' | 'Likes' | 'Reposts' | 'Replies' | 'Quotes';
export type EngagementLevel = 'Unknown' | 'Low' | 'Medium' | 'High';
export type PostSort = 'Recent' | 'MostEngaged' | 'HighestRate';
export type SeriesGranularity = 'Hour' | 'Day';
export type RefreshStatus =
  | 'Pending'
  | 'Success'
  | 'NoResults'
  | 'Deferred'
  | 'RateLimited'
  | 'AuthFailed'
  | 'ConnectionFailed'
  | 'NotConfigured';
export type QuotaMode = 'Normal' | 'Throttled' | 'Critical' | 'Denied';
export type RefreshJobType = 'Discovery' | 'MetricsRefresh' | 'Retention';

export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface SignInResponse {
  accessToken: string;
  expiresUtc: string;
  user: User;
}

export interface RuleInput {
  type: RuleType;
  value: string;
  isExclusion: boolean;
}

export interface Rule extends RuleInput {
  id: string;
  displayValue: string;
}

export interface Topic {
  id: string;
  name: string;
  description: string | null;
  priority: TopicPriority;
  isActive: boolean;
  rules: Rule[];
  generatedQuery: string;
  postCount: number;
  lastRefreshStatus: RefreshStatus;
  lastRefreshAttemptUtc: string | null;
  lastSuccessfulRefreshUtc: string | null;
  isStale: boolean;
  refreshIntervalMinutes: number;
  createdUtc: string;
  createdBy: string;
  updatedUtc: string | null;
  version: string;
}

export interface TopicRequest {
  name: string;
  description: string | null;
  priority: TopicPriority;
  rules: RuleInput[];
}

export interface UpdateTopicRequest extends TopicRequest {
  version: string;
}

export interface QueryPreview {
  query: string;
  length: number;
  maxLength: number;
  isWithinLimit: boolean;
}

export interface RefreshRequested {
  topicId: string;
  jobId: string;
  message: string;
}

export interface DataFreshness {
  source: DataSource | null;
  sourceLabel: string;
  isSampleData: boolean;
  status: RefreshStatus;
  isStale: boolean;
  lastUpdatedUtc: string | null;
  lastAttemptUtc: string | null;
  retryAfterUtc: string | null;
  message: string;
  generatedUtc: string;
}

export interface AppliedFilter {
  fromUtc: string;
  toUtc: string;
  keyword: string | null;
  engagementType: EngagementType;
  level: EngagementLevel | null;
  granularity: SeriesGranularity;
  usesPreAggregatedData: boolean;
}

export interface Kpis {
  posts: number;
  likes: number;
  reposts: number;
  replies: number;
  quotes: number;
  interactions: number;
  views: number | null;
  engagementRate: number | null;
  engagementLevel: EngagementLevel;
  postsWithViews: number;
  selectedTypeTotal: number;
}

export interface SeriesPoint {
  bucketStartUtc: string;
  posts: number;
  value: number;
  interactions: number;
  views: number;
  engagementRate: number | null;
  likes: number;
  reposts: number;
  replies: number;
  quotes: number;
  isSpike: boolean;
}

/** Change versus the previous period of equal length, in percent (rate: percentage points). */
export interface Trends {
  previousFromUtc: string;
  previousToUtc: string;
  previousPosts: number;
  /** False when the previous period predates the topic's data coverage: changes are then null ("new"). */
  isComparable: boolean;
  posts: number | null;
  interactions: number | null;
  selectedType: number | null;
  likes: number | null;
  reposts: number | null;
  replies: number | null;
  quotes: number | null;
  views: number | null;
  engagementRatePoints: number | null;
}

export interface EngagementBreakdown {
  likes: number;
  reposts: number;
  replies: number;
  quotes: number;
}

export interface LevelDistribution {
  high: number;
  medium: number;
  low: number;
  unknown: number;
}

export interface Author {
  id: string;
  username: string;
  displayName: string;
  profileImageUrl: string | null;
  isVerified: boolean;
}

export interface PostMetrics {
  likes: number;
  reposts: number;
  replies: number;
  quotes: number;
  views: number | null;
  interactions: number;
}

export interface PostMediaInfo {
  kind: string;
  previewUrl: string;
  altText: string | null;
}

export interface Post {
  id: string;
  text: string;
  language: string | null;
  createdAtUtc: string;
  url: string | null;
  source: DataSource;
  author: Author;
  metrics: PostMetrics;
  engagementRate: number | null;
  engagementLevel: EngagementLevel;
  metricsCapturedUtc: string;
  media: PostMediaInfo | null;
}

export interface EngagementMethod {
  interactionsFormula: string;
  rateFormula: string;
  topicRateFormula: string;
  mediumFromPercent: number;
  highAbovePercent: number;
  notes: string;
}

export interface Dashboard {
  topic: Topic;
  freshness: DataFreshness;
  filter: AppliedFilter;
  kpis: Kpis;
  trends: Trends;
  series: SeriesPoint[];
  breakdown: EngagementBreakdown;
  levels: LevelDistribution;
  topPosts: Post[];
  method: EngagementMethod;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface DashboardFilter {
  fromUtc: string | null;
  toUtc: string | null;
  keyword: string | null;
  engagementType: EngagementType;
  level: EngagementLevel | null;
}

export interface QuotaStatus {
  period: string;
  postsRead: number;
  requests: number;
  monthlyBudget: number;
  budgetUsedPercent: number | null;
  mode: QuotaMode;
  reason: string;
  rateLimitRemaining: number | null;
  rateLimitResetUtc: string | null;
}

export interface RefreshRun {
  id: number;
  topicId: string | null;
  topicName: string | null;
  jobType: RefreshJobType;
  source: DataSource | null;
  status: RefreshStatus;
  startedUtc: string;
  finishedUtc: string | null;
  postsProcessed: number;
  apiCalls: number;
  message: string | null;
}

export interface SystemStatus {
  activeSource: DataSource | null;
  sourceLabel: string;
  sampleDataAllowed: boolean;
  quota: QuotaStatus;
  recentRuns: RefreshRun[];
  generatedUtc: string;
}

/** RFC 9457 problem details returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

// ── Insights ───────────────────────────────────────────────────────────────

export interface Highlight {
  icon: string;
  title: string;
  detail: string;
}

export interface HashtagInsight {
  tag: string;
  posts: number;
  interactions: number;
  sharePercent: number;
}

export interface AccountInsight {
  authorId: string;
  username: string;
  displayName: string;
  profileImageUrl: string | null;
  isVerified: boolean;
  posts: number;
  interactions: number;
  engagementRate: number | null;
  interactionSharePercent: number;
}

export interface ActivityHeatmap {
  /** [weekday 0 = Sunday … 6][hour 0–23], viewer's local time. */
  cells: number[][];
  max: number;
  peakDay: number;
  peakHour: number;
}

export interface RuleInsight {
  type: RuleType;
  displayValue: string;
  posts: number;
  interactions: number;
  sharePercent: number;
}

export interface LanguageInsight {
  code: string;
  label: string;
  posts: number;
  sharePercent: number;
}

export interface TopicInsights {
  postsAnalysed: number;
  isTruncated: boolean;
  highlights: Highlight[];
  topHashtags: HashtagInsight[];
  topAccounts: AccountInsight[];
  heatmap: ActivityHeatmap;
  rules: RuleInsight[];
  languages: LanguageInsight[];
}

// ── Overview & alerts ──────────────────────────────────────────────────────

export interface OverviewTotals {
  topics: number;
  activeTopics: number;
  posts: number;
  interactions: number;
  views: number | null;
  engagementRate: number | null;
  postsChange: number | null;
  interactionsChange: number | null;
  viewsChange: number | null;
  engagementRatePoints: number | null;
}

export interface TopicOverview {
  id: string;
  name: string;
  priority: TopicPriority;
  isActive: boolean;
  status: RefreshStatus;
  isStale: boolean;
  lastSuccessfulRefreshUtc: string | null;
  posts: number;
  interactions: number;
  views: number | null;
  engagementRate: number | null;
  engagementLevel: EngagementLevel;
  postsChange: number | null;
  interactionsChange: number | null;
  postsByDay: number[];
  interactionsByDay: number[];
}

export interface Overview {
  generatedUtc: string;
  source: DataSource | null;
  sourceLabel: string;
  lastUpdatedUtc: string | null;
  totals: OverviewTotals;
  days: string[];
  topics: TopicOverview[];
}

export type AlertSeverity = 'Info' | 'Warning' | 'Critical';
export type AlertKind = 'ActivitySpike' | 'CollectionProblem' | 'StaleData' | 'Quota';

export interface Alert {
  id: string;
  kind: AlertKind;
  severity: AlertSeverity;
  title: string;
  message: string;
  topicId: string | null;
  topicName: string | null;
  occurredUtc: string;
}
