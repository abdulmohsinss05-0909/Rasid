import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, model } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatDateRangeInput, MatDateRangePicker, MatDatepickerToggle, MatEndDate, MatStartDate } from '@angular/material/datepicker';
import { MatFormField, MatLabel, MatPrefix, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { EngagementLevel, EngagementType } from '../../core/models/api.models';
import { EngagementTypeOptions, FilterState, LevelOptions, Period, PeriodOptions } from './dashboard-filter';

/** Filters: period, keyword, engagement type and level. Two-way bound to the page's filter state. */
@Component({
  selector: 'rasid-dashboard-filter-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule, MatButtonToggleGroup, MatButtonToggle, MatFormField, MatLabel, MatPrefix, MatSuffix, MatInput,
    MatSelect, MatOption, MatDateRangeInput, MatStartDate, MatEndDate, MatDatepickerToggle, MatDateRangePicker,
  ],
  template: `
    @let f = filter();
    <div class="bar">
      <div class="row">
        <mat-button-toggle-group class="scroll" [value]="f.period" (change)="setPeriod($event.value)" aria-label="Period" hideSingleSelectionIndicator>
          @for (p of periods; track p.value) {
            <mat-button-toggle [value]="p.value">{{ p.label }}</mat-button-toggle>
          }
        </mat-button-toggle-group>

        @if (f.period === 'custom') {
          <mat-form-field appearance="outline" class="dates" subscriptSizing="dynamic">
            <mat-label>Date range</mat-label>
            <mat-date-range-input [rangePicker]="picker" [max]="today">
              <input matStartDate placeholder="Start" [value]="f.customFrom" (dateChange)="patch({ customFrom: $event.value })" />
              <input matEndDate placeholder="End" [value]="f.customTo" (dateChange)="patch({ customTo: $event.value })" />
            </mat-date-range-input>
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-date-range-picker #picker />
          </mat-form-field>
        }
      </div>

      <div class="row">
        <mat-form-field appearance="outline" class="search" subscriptSizing="dynamic">
          <span matPrefix class="material-symbols-rounded prefix">search</span>
          <mat-label>Filter by keyword</mat-label>
          <input matInput [formControl]="keyword" maxlength="100" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="level" subscriptSizing="dynamic">
          <mat-label>Engagement level</mat-label>
          <mat-select [value]="f.level" (selectionChange)="patch({ level: $event.value })">
            @for (l of levels; track l.label) {
              <mat-option [value]="l.value">{{ l.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      <mat-button-toggle-group class="scroll types" [value]="f.engagementType" (change)="setType($event.value)" aria-label="Engagement type" hideSingleSelectionIndicator>
        @for (t of types; track t.value) {
          <mat-button-toggle [value]="t.value">
            <span class="material-symbols-rounded type-icon" aria-hidden="true">{{ t.icon }}</span>{{ t.label }}
          </mat-button-toggle>
        }
      </mat-button-toggle-group>
    </div>
  `,
  styles: `
    .bar { display: grid; gap: 12px; }
    .row { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; }
    .scroll { max-width: 100%; overflow-x: auto; scrollbar-width: none; }
    .search { flex: 1 1 260px; }
    .level { flex: 0 1 220px; }
    .dates { flex: 0 1 300px; }
    .prefix { padding: 0 6px 0 12px; color: var(--r-text-subtle); }
    .type-icon { font-size: 18px; vertical-align: middle; margin-right: 6px; }
    @media (max-width: 599.98px) { .level, .dates { flex: 1 1 100%; } }
  `,
})
export class DashboardFilterBar implements OnInit {
  readonly filter = model.required<FilterState>();

  protected readonly periods = PeriodOptions;
  protected readonly types = EngagementTypeOptions;
  protected readonly levels = LevelOptions;
  protected readonly today = new Date();
  protected readonly keyword = new FormControl('', { nonNullable: true });

  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.keyword.setValue(this.filter().keyword, { emitEvent: false });
    this.keyword.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((keyword) => this.patch({ keyword }));
  }

  protected setPeriod(period: Period): void {
    this.patch({ period });
  }

  protected setType(engagementType: EngagementType): void {
    this.patch({ engagementType });
  }

  protected patch(changes: Partial<FilterState> & { level?: EngagementLevel | null }): void {
    this.filter.update((current) => ({ ...current, ...changes }));
  }
}
