import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatProgressBar } from '@angular/material/progress-bar';
import { PagedResult, Post, PostSort } from '../../core/models/api.models';
import { PostCard } from '../../shared/ui/post-card';

const SortOptions: { value: PostSort; label: string }[] = [
  { value: 'Recent', label: 'Most recent' },
  { value: 'MostEngaged', label: 'Most engaged' },
  { value: 'HighestRate', label: 'Highest engagement rate' },
];

/** Paged, sortable list of the topic's posts for the current filter. */
@Component({
  selector: 'rasid-posts-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PostCard, MatPaginator, MatSelect, MatOption, MatFormField, MatLabel, MatProgressBar],
  template: `
    <section class="r-card">
      <div class="head">
        <h2 class="r-card__title">Posts <span class="r-chip">{{ result()?.totalCount ?? 0 }}</span></h2>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="sort">
          <mat-label>Sort by</mat-label>
          <mat-select [value]="sort()" (selectionChange)="sortChange.emit($event.value)">
            @for (o of sortOptions; track o.value) {
              <mat-option [value]="o.value">{{ o.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (result(); as r) {
        @if (r.items.length === 0) {
          <div class="r-state">
            <span class="material-symbols-rounded">filter_alt_off</span>
            <h2>No posts match these filters</h2>
            <p>Try a longer period, another keyword or a different engagement level.</p>
          </div>
        } @else {
          <div class="list">
            @for (post of r.items; track post.id) {
              <rasid-post-card [post]="post" />
            }
          </div>
          <mat-paginator
            [length]="r.totalCount"
            [pageIndex]="r.page - 1"
            [pageSize]="r.pageSize"
            [pageSizeOptions]="[10, 20, 50]"
            (page)="onPage($event)"
            aria-label="Posts pages"
          />
        }
      }
    </section>
  `,
  styles: `
    .head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
    .head .r-card__title { margin: 0; }
    .sort { width: 230px; max-width: 100%; }
    .list > * + * { border-top: 1px solid var(--r-border); }
    mat-paginator { background: transparent; }
  `,
})
export class PostsPanel {
  readonly result = input<PagedResult<Post> | null>(null);
  readonly sort = input.required<PostSort>();
  readonly loading = input(false);
  readonly sortChange = output<PostSort>();
  readonly pageChange = output<{ page: number; pageSize: number }>();

  protected readonly sortOptions = SortOptions;

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
