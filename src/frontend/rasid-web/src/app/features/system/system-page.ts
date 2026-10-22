import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatProgressBar } from '@angular/material/progress-bar';
import { toApiError } from '../../core/api/api-error';
import { SystemStatus } from '../../core/models/api.models';
import { SystemService } from '../../core/services/system.service';
import { IntegerPipe, LocalDateTimePipe, RelativeTimePipe } from '../../shared/formatting/format.pipes';
import { SourceBadge, StatusChip } from '../../shared/ui/chips';

/** Operations view for administrators: data source, X API quota and recent collection runs. */
@Component({
  selector: 'rasid-system-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButton, MatProgressBar, SourceBadge, StatusChip, IntegerPipe, LocalDateTimePipe, RelativeTimePipe],
  template: `
    <div class="r-page">
      <header class="r-page-header">
        <div>
          <h1>System status</h1>
          <p>Data source, X API budget and the latest collection runs.</p>
        </div>
        <div class="r-actions">
          <button matButton="outlined" type="button" (click)="load()">
            <span class="material-symbols-rounded btn-icon">refresh</span> Reload
          </button>
        </div>
      </header>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (error(); as message) {
        <div class="r-card r-state">
          <span class="material-symbols-rounded">cloud_off</span>
          <h2>Status unavailable</h2>
          <p>{{ message }}</p>
        </div>
      }

      @if (status(); as s) {
        <div class="cards">
          <section class="r-card">
            <h2 class="r-card__title">Data source</h2>
            <rasid-source-badge [source]="s.activeSource" />
            <p class="r-muted">{{ s.sourceLabel }}</p>
            <p class="r-subtle small">Sample data {{ s.sampleDataAllowed ? 'is allowed' : 'is disabled' }} in this environment.</p>
          </section>

          <section class="r-card">
            <h2 class="r-card__title">X API budget · {{ s.quota.period }}</h2>
            <div class="quota">
              <strong>{{ s.quota.postsRead | integer }}</strong>
              <span class="r-muted">posts read{{ s.quota.monthlyBudget > 0 ? ' of ' + (s.quota.monthlyBudget | integer) : '' }}</span>
            </div>
            @if (s.quota.monthlyBudget > 0) {
              <div class="meter" [class]="'meter meter--' + s.quota.mode.toLowerCase()">
                <span [style.width.%]="usage()"></span>
              </div>
            }
            <p class="small"><span class="r-chip" [class.r-chip--success]="s.quota.mode === 'Normal'" [class.r-chip--warning]="s.quota.mode !== 'Normal'">{{ s.quota.mode }}</span> {{ s.quota.reason }}</p>
            @if (s.quota.rateLimitRemaining !== null) {
              <p class="r-subtle small">Rate-limit window: {{ s.quota.rateLimitRemaining }} requests left, resets {{ s.quota.rateLimitResetUtc | relativeTime }}.</p>
            }
          </section>
        </div>

        <section class="r-card">
          <h2 class="r-card__title">Recent collection runs</h2>
          <div class="table-wrap">
            <table>
              <thead>
                <tr><th>Started</th><th>Job</th><th>Topic</th><th>Status</th><th class="num">Posts</th><th class="num">API calls</th><th>Note</th></tr>
              </thead>
              <tbody>
                @for (run of s.recentRuns; track run.id) {
                  <tr>
                    <td class="r-nowrap" [title]="run.startedUtc | localDateTime">{{ run.startedUtc | relativeTime }}</td>
                    <td>{{ run.jobType }}</td>
                    <td>{{ run.topicName ?? '—' }}</td>
                    <td><rasid-status-chip [status]="run.status" /></td>
                    <td class="num">{{ run.postsProcessed | integer }}</td>
                    <td class="num">{{ run.apiCalls }}</td>
                    <td class="note">{{ run.message ?? '' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="r-muted">No runs yet.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }
    </div>
  `,
  styles: `
    .r-page { display: grid; gap: 16px; }
    .r-page-header { margin-bottom: 0; }
    .btn-icon { font-size: 20px; }
    .cards { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 320px), 1fr)); }
    .small { font-size: 0.82rem; margin: 10px 0 0; }
    .quota { display: flex; align-items: baseline; gap: 8px; strong { font-size: 1.8rem; } }
    .meter { height: 8px; margin-top: 12px; border-radius: 999px; background: var(--r-surface-2); overflow: hidden; }
    .meter span { display: block; height: 100%; background: var(--r-success); }
    .meter--throttled span { background: var(--r-warning); }
    .meter--critical span, .meter--denied span { background: var(--r-danger); }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    th { text-align: left; font-weight: 600; color: var(--r-text-muted); padding: 8px 10px; border-bottom: 1px solid var(--r-border); white-space: nowrap; }
    td { padding: 10px; border-bottom: 1px solid var(--r-border); vertical-align: middle; }
    .num { text-align: right; }
    .note { color: var(--r-text-muted); min-width: 220px; }
  `,
})
export class SystemPage implements OnInit {
  private readonly systemService = inject(SystemService);

  protected readonly status = signal<SystemStatus | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly usage = computed(() => Math.min(100, this.status()?.quota.budgetUsedPercent ?? 0));

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.systemService.getStatus().subscribe({
      next: (status) => {
        this.status.set(status);
        this.error.set(null);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(toApiError(error).message);
        this.loading.set(false);
      },
    });
  }
}
