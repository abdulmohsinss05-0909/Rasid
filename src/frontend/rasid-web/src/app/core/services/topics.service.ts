import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { ApiEndpoints } from '../config/api-endpoints';
import {
  QueryPreview,
  RefreshRequested,
  RuleInput,
  Topic,
  TopicRequest,
  UpdateTopicRequest,
} from '../models/api.models';

/** Data access for monitoring topics. Components never call HTTP directly. */
@Injectable({ providedIn: 'root' })
export class TopicsService {
  private readonly api = inject(ApiClient);

  list(): Observable<Topic[]> {
    return this.api.get<Topic[]>(ApiEndpoints.topics.list);
  }

  get(id: string): Observable<Topic> {
    return this.api.get<Topic>(ApiEndpoints.topics.byId(id));
  }

  create(request: TopicRequest): Observable<Topic> {
    return this.api.post<Topic>(ApiEndpoints.topics.list, request);
  }

  update(id: string, request: UpdateTopicRequest): Observable<Topic> {
    return this.api.put<Topic>(ApiEndpoints.topics.byId(id), request);
  }

  setActive(id: string, active: boolean): Observable<Topic> {
    return this.api.post<Topic>(active ? ApiEndpoints.topics.resume(id) : ApiEndpoints.topics.pause(id));
  }

  delete(id: string): Observable<void> {
    return this.api.delete(ApiEndpoints.topics.byId(id));
  }

  requestRefresh(id: string): Observable<RefreshRequested> {
    return this.api.post<RefreshRequested>(ApiEndpoints.topics.refresh(id));
  }

  previewQuery(rules: RuleInput[]): Observable<QueryPreview> {
    return this.api.post<QueryPreview>(ApiEndpoints.topics.previewQuery, { rules });
  }
}
