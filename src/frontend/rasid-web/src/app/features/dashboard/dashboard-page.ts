import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle } from '@angular/material/expansion';
import { MatFormField } from '@angular/material/form-field';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatTooltip } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, filter, finalize, interval, map, of, switchMap, tap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { Dashboard, PagedResult, Post, PostSort, Topic, TopicInsights } from '../../core/models/api.models';
import { Notifier } from '../../core/notifications/notifier';
import { DashboardService } from '../../core/services/dashboard.service';
import { TopicsService } from '../../core/services/topics.service';
import { Gauge } from '../../shared/ui/gauge';
import { KpiCard } from '../../shared/ui/kpi-card';
import { PostCard } from '../../shared/ui/post-card';
import { TrendChip } from '../../shared/ui/trend-chip';
import { BreakdownChart, EngagementTrendChart, LevelsChart } from './dashboard-charts';
import { DefaultFilter, FilterState, PeriodOptions, engagementTypeLabel, toApiFilter } from './dashboard-filter';
import { DashboardFilterBar } from './dashboard-filter-bar';
import { FreshnessBanner } from './freshness-banner';
import { ActivityHeatmapChart, InsightHighlights, RankedItem, RankedList } from './insight-panels';
import { PostsPanel } from './posts-panel';

const AutoReloadMs = 60_000;
const compact = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });

/**
 * Topic analytics. Filter changes reload the summary, the insights and the post list in parallel;
 * stale requests are cancelled (switchMap). The page re-reads data every minute while visible.
 */
@Component({
  selector: 'rasid-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, MatButton, MatProgressBar, MatTooltip, MatSelect, MatOption, MatFormField,
    MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle,
    DashboardFilterBar, FreshnessBanner, KpiCard, Gauge, TrendChip, PostCard, PostsPanel,
    EngagementTrendChart, BreakdownChart, LevelsChart, InsightHighlights, RankedList, ActivityHeatmapChart,
  ],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly dashboardService = inject(DashboardService);
  private readonly topicsService = inject(TopicsService);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  /** Route parameter. */
  readonly id = input.required<string>();

  protected readonly filter = signal<FilterState>(DefaultFilter);
  protected readonly sort = signal<PostSort>('Recent');
  protected readonly page = signal({ page: 1, pageSize: 10 });
  protected readonly filtersOpen = signal(false);
  private readonly reloadTick = signal(0);

  protected readonly dashboard = signal<Dashboard | null>(null);
  protected readonly insights = signal<TopicInsights | null>(null);
  protected readonly posts = signal<PagedResult<Post> | null>(null);
  protected readonly topics = signal<Topic[]>([]);
  protected readonly loading = signal(true);
  protected readonly postsLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly refreshing = signal(false);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map((s) => s.matches)),
    { initialValue: false },
  );

  protected readonly selectedLabel = computed(() => engagementTypeLabel(this.filter().engagementType));
  protected readonly periodLabel = computed(() => {
    const option = PeriodOptions.find((p) => p.value === this.filter().period);
    return this.filter().period === 'custom' ? 'previous period' : `previous ${option?.label ?? 'period'}`;
  });
  protected readonly activeFilterCount = computed(() => {
    const f = this.filter();
    return [f.keyword.trim() !== '', f.level !== null, f.engagementType !== 'All', f.period !== DefaultFilter.period].filter(Boolean).length;
  });

  protected readonly spark = computed(() => {
    const series = this.dashboard()?.series ?? [];
    return {
      posts: series.map((p) => p.posts),
      value: series.map((p) => p.value),
      likes: series.map((p) => p.likes),
      reposts: series.map((p) => p.reposts),
      replies: series.map((p) => p.replies),
      views: series.map((p) => p.views),
    };
  });

  protected readonly coverage = computed(() => {
    const k = this.dashboard()?.kpis;
    return k ? `Based on ${k.postsWithViews} of ${k.posts} posts with view counts` : '';
  });

  protected readonly hashtagItems = computed<RankedItem[]>(() =>
    (this.insights()?.topHashtags ?? []).map((h) => ({
      key: h.tag,
      label: `#${h.tag}`,
      sublabel: `${compact.format(h.interactions)} interactions`,
      value: `${h.sharePercent}%`,
      share: h.sharePercent,
    })));

  protected readonly accountItems = computed<RankedItem[]>(() =>
    (this.insights()?.topAccounts ?? []).map((a) => ({
      key: a.authorId,
      label: a.displayName,
      sublabel: `@${a.username} · ${a.posts} post${a.posts === 1 ? '' : 's'}${a.engagementRate !== null ? ` · ${a.engagementRate.toFixed(2)}% rate` : ''}`,
      value: compact.format(a.interactions),
      share: a.interactionSharePercent,
      initials: initials(a.displayName),
      imageUrl: a.profileImageUrl,
    })));

  protected readonly ruleItems = computed<RankedItem[]>(() =>
    (this.insights()?.rules ?? []).map((r) => ({
      key: r.type + r.displayValue,
      label: r.displayValue,
      sublabel: `${r.type} · ${compact.format(r.interactions)} interactions`,
      value: `${r.posts} posts`,
      share: r.sharePercent,
    })));

  protected readonly languageItems = computed<RankedItem[]>(() =>
    (this.insights()?.languages ?? []).map((l) => ({
      key: l.code,
      label: l.label,
      sublabel: `${l.posts} posts`,
      value: `${l.sharePercent}%`,
      share: l.sharePercent,
    })));

  constructor() {
    this.topicsService.list().pipe(takeUntilDestroyed()).subscribe({ next: (t) => this.topics.set(t), error: () => undefined });

    const apiFilter$ = combineLatest([toObservable(this.id), toObservable(this.filter), toObservable(this.reloadTick)]).pipe(
      map(([id, state]) => ({ id, filter: toApiFilter(state) })),
    );

    apiFilter$
      .pipe(
        tap(() => this.loading.set(this.dashboard() === null)),
        switchMap(({ id, filter: f }) =>
          this.dashboardService.getDashboard(id, f).pipe(
            catchError((error: unknown) => {
              this.error.set(toApiError(error).message);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((dashboard) => {
        if (dashboard) {
          this.dashboard.set(dashboard);
          this.error.set(null);
        }
        this.loading.set(false);
      });

    apiFilter$
      .pipe(
        switchMap(({ id, filter: f }) => this.dashboardService.getInsights(id, f).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((insights) => {
        if (insights) {
          this.insights.set(insights);
        }
      });

    toObservable(this.filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.page.update((p) => ({ ...p, page: 1 })));

    combineLatest([apiFilter$, toObservable(this.sort), toObservable(this.page)])
      .pipe(
        tap(() => this.postsLoading.set(true)),
        switchMap(([{ id, filter: f }, sort, page]) =>
          this.dashboardService.getPosts(id, f, sort, page.page, page.pageSize).pipe(
            catchError(() => of(null)),
            finalize(() => this.postsLoading.set(false)),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((posts) => {
        if (posts) {
          this.posts.set(posts);
        }
        this.postsLoading.set(false);
      });

    interval(AutoReloadMs)
      .pipe(
        filter(() => document.visibilityState === 'visible'),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.reload());
  }

  protected reload(): void {
    this.reloadTick.update((tick) => tick + 1);
  }

  protected switchTopic(topicId: string): void {
    if (topicId !== this.id()) {
      this.dashboard.set(null);
      this.insights.set(null);
      void this.router.navigate(['/topics', topicId]);
    }
  }

  protected requestRefresh(): void {
    this.refreshing.set(true);
    this.topicsService.requestRefresh(this.id()).subscribe({
      next: (result) => {
        this.notifier.success(result.message);
        setTimeout(() => {
          this.refreshing.set(false);
          this.reload();
        }, 8000);
      },
      error: (error: unknown) => {
        this.refreshing.set(false);
        this.notifier.error(error);
      },
    });
  }

  protected copyQuery(query: string): void {
    void navigator.clipboard?.writeText(query).then(() => this.notifier.success('Query copied.'));
  }
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join('');
}
