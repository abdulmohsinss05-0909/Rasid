import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { ApiEndpoints } from '../config/api-endpoints';
import { SystemStatus } from '../models/api.models';

/** Data access for operational status (administrators only). */
@Injectable({ providedIn: 'root' })
export class SystemService {
  private readonly api = inject(ApiClient);

  getStatus(): Observable<SystemStatus> {
    return this.api.get<SystemStatus>(ApiEndpoints.system.status);
  }
}
