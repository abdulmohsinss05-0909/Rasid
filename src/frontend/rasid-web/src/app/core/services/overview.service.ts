import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { ApiEndpoints } from '../config/api-endpoints';
import { Alert, Overview } from '../models/api.models';
import { localUtcOffsetMinutes } from './dashboard.service';

/** Data access for the cross-topic overview and current alerts. */
@Injectable({ providedIn: 'root' })
export class OverviewService {
  private readonly api = inject(ApiClient);

  getOverview(): Observable<Overview> {
    return this.api.get<Overview>(ApiEndpoints.overview, { utcOffsetMinutes: localUtcOffsetMinutes() });
  }

  getAlerts(): Observable<Alert[]> {
    return this.api.get<Alert[]>(ApiEndpoints.alerts);
  }
}
