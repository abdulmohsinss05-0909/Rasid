import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { ApiEndpoints } from '../config/api-endpoints';
import { Dashboard, DashboardFilter, PagedResult, Post, PostSort, TopicInsights } from '../models/api.models';

/** Data access for topic analytics: dashboard summary and the paged post list. */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiClient);

  getDashboard(topicId: string, filter: DashboardFilter): Observable<Dashboard> {
    return this.api.get<Dashboard>(ApiEndpoints.topics.dashboard(topicId), {
      ...toQuery(filter),
      // Day buckets follow the viewer's local day.
      utcOffsetMinutes: localUtcOffsetMinutes(),
    });
  }

  /** Hashtags, accounts, heatmap, rules, languages and highlights for the same filter. */
  getInsights(topicId: string, filter: DashboardFilter): Observable<TopicInsights> {
    return this.api.get<TopicInsights>(ApiEndpoints.topics.insights(topicId), {
      ...toQuery(filter),
      engagementType: null,
      utcOffsetMinutes: localUtcOffsetMinutes(),
    });
  }

  getPosts(topicId: string, filter: DashboardFilter, sort: PostSort, page: number, pageSize: number): Observable<PagedResult<Post>> {
    return this.api.get<PagedResult<Post>>(ApiEndpoints.topics.posts(topicId), {
      ...toQuery(filter),
      sort,
      page,
      pageSize,
    });
  }
}

function toQuery(filter: DashboardFilter): Record<string, string | null> {
  return {
    fromUtc: filter.fromUtc,
    toUtc: filter.toUtc,
    keyword: filter.keyword?.trim() || null,
    engagementType: filter.engagementType,
    level: filter.level,
  };
}

export function localUtcOffsetMinutes(): number {
  return -new Date().getTimezoneOffset();
}
