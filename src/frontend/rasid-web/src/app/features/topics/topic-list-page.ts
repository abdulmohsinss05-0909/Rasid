import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTooltip } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, interval, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { Topic } from '../../core/models/api.models';
import { Notifier } from '../../core/notifications/notifier';
import { TopicsService } from '../../core/services/topics.service';
import { CompactNumberPipe, LocalDateTimePipe, RelativeTimePipe } from '../../shared/formatting/format.pipes';
import { StatusChip } from '../../shared/ui/chips';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/ui/confirm-dialog';

const AutoRefreshMs = 30_000;

@Component({
  selector: 'rasid-topic-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButton, MatIconButton, MatMenu, MatMenuItem, MatMenuTrigger, MatTooltip, StatusChip, CompactNumberPipe, RelativeTimePipe, LocalDateTimePipe],
  templateUrl: './topic-list-page.html',
  styleUrl: './topic-list-page.scss',
})
export class TopicListPage implements OnInit {
  private readonly topicsService = inject(TopicsService);
  private readonly notifier = inject(Notifier);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly topics = signal<Topic[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly busyTopicId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();

    // Keep collection status current while the page is open (paused when the tab is hidden).
    interval(AutoRefreshMs)
      .pipe(
        filter(() => document.visibilityState === 'visible'),
        switchMap(() => this.topicsService.list()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ next: (topics) => this.topics.set(topics), error: () => undefined });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.topicsService.list().subscribe({
      next: (topics) => {
        this.topics.set(topics);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(toApiError(error).message);
        this.loading.set(false);
      },
    });
  }

  protected refreshNow(topic: Topic): void {
    this.busyTopicId.set(topic.id);
    this.topicsService.requestRefresh(topic.id).subscribe({
      next: (result) => {
        this.notifier.success(result.message);
        this.busyTopicId.set(null);
      },
      error: (error: unknown) => {
        this.notifier.error(error);
        this.busyTopicId.set(null);
      },
    });
  }

  protected toggleActive(topic: Topic): void {
    this.busyTopicId.set(topic.id);
    this.topicsService.setActive(topic.id, !topic.isActive).subscribe({
      next: (updated) => {
        this.replace(updated);
        this.notifier.success(updated.isActive ? `"${updated.name}" resumed.` : `"${updated.name}" paused.`);
        this.busyTopicId.set(null);
      },
      error: (error: unknown) => {
        this.notifier.error(error);
        this.busyTopicId.set(null);
      },
    });
  }

  protected confirmDelete(topic: Topic): void {
    const data: ConfirmDialogData = {
      title: `Delete "${topic.name}"?`,
      message: 'The topic, its aggregated metrics and any posts no other topic uses will be permanently removed.',
      confirmLabel: 'Delete topic',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data, width: '440px' })
      .afterClosed()
      .pipe(
        filter((confirmed: unknown) => confirmed === true),
        switchMap(() => this.topicsService.delete(topic.id)),
      )
      .subscribe({
        next: () => {
          this.topics.update((list) => list.filter((t) => t.id !== topic.id));
          this.notifier.success(`"${topic.name}" deleted.`);
        },
        error: (error: unknown) => this.notifier.error(error),
      });
  }

  private replace(updated: Topic): void {
    this.topics.update((list) => list.map((t) => (t.id === updated.id ? updated : t)));
  }
}
