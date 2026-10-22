import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatChipGrid, MatChipInput, MatChipInputEvent, MatChipRemove, MatChipRow } from '@angular/material/chips';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, of, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { QueryPreview, RuleInput, RuleType, TopicPriority } from '../../core/models/api.models';
import { Notifier } from '../../core/notifications/notifier';
import { TopicsService } from '../../core/services/topics.service';
import { displayRule, normalizeRule } from './rule-rules';

interface RuleGroup {
  key: string;
  type: RuleType;
  isExclusion: boolean;
  label: string;
  placeholder: string;
  hint: string;
  icon: string;
}

const RuleGroups: RuleGroup[] = [
  { key: 'keywords', type: 'Keyword', isExclusion: false, label: 'Keywords', placeholder: 'e.g. humanitarian aid', hint: 'Exact phrases. Press Enter after each.', icon: 'text_fields' },
  { key: 'hashtags', type: 'Hashtag', isExclusion: false, label: 'Hashtags', placeholder: 'e.g. #UAEAid', hint: 'With or without #.', icon: 'tag' },
  { key: 'accounts', type: 'Account', isExclusion: false, label: 'Public accounts', placeholder: 'e.g. @account', hint: 'Posts from these accounts.', icon: 'alternate_email' },
  { key: 'excluded', type: 'Keyword', isExclusion: true, label: 'Exclude keywords', placeholder: 'e.g. giveaway', hint: 'Posts containing these are ignored.', icon: 'block' },
];

@Component({
  selector: 'rasid-topic-form-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule, RouterLink, MatFormField, MatLabel, MatHint, MatError, MatInput, MatButton,
    MatButtonToggleGroup, MatButtonToggle, MatChipGrid, MatChipRow, MatChipRemove, MatChipInput, MatProgressBar,
  ],
  templateUrl: './topic-form-page.html',
  styleUrl: './topic-form-page.scss',
})
export class TopicFormPage implements OnInit {
  private readonly topicsService = inject(TopicsService);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Route parameter (edit mode when present). */
  readonly id = input<string>();

  protected readonly groups = RuleGroups;
  protected readonly separatorKeys = [ENTER, COMMA] as const;
  protected readonly priorities: TopicPriority[] = ['Low', 'Normal', 'High'];

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    priority: ['Normal' as TopicPriority, Validators.required],
  });

  protected readonly rules = signal<RuleInput[]>([]);
  protected readonly ruleErrors = signal<Record<string, string>>({});
  protected readonly serverRuleErrors = signal<string[]>([]);
  protected readonly preview = signal<QueryPreview | null>(null);
  protected readonly previewError = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  private version = '';

  protected readonly isEdit = computed(() => !!this.id());
  protected readonly hasInclusion = computed(() => this.rules().some((r) => !r.isExclusion));
  protected readonly previewUsage = computed(() => {
    const p = this.preview();
    return p ? Math.min(100, Math.round((p.length / p.maxLength) * 100)) : 0;
  });

  constructor() {
    // Live query preview, debounced, built by the server so it is exactly what X will receive.
    toObservable(this.rules)
      .pipe(
        debounceTime(350),
        switchMap((rules) => {
          if (!rules.some((r) => !r.isExclusion)) {
            return of(null);
          }
          return this.topicsService.previewQuery(rules).pipe(
            catchError((error: unknown) => {
              this.previewError.set(toApiError(error).message);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((preview) => {
        this.preview.set(preview);
        if (preview) {
          this.previewError.set(null);
        }
      });
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.topicsService.get(id).subscribe({
      next: (topic) => {
        this.form.setValue({ name: topic.name, description: topic.description ?? '', priority: topic.priority });
        this.rules.set(topic.rules.map(({ type, value, isExclusion }) => ({ type, value, isExclusion })));
        this.version = topic.version;
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.notifier.error(error);
        void this.router.navigate(['/topics']);
      },
    });
  }

  protected rulesOf(group: RuleGroup): RuleInput[] {
    return this.rules().filter((r) => r.type === group.type && r.isExclusion === group.isExclusion);
  }

  protected display(rule: RuleInput): string {
    return displayRule(rule.type, rule.value);
  }

  protected addRule(group: RuleGroup, event: MatChipInputEvent): void {
    const raw = event.value.trim();
    if (!raw) {
      return;
    }

    const result = normalizeRule(group.type, raw);
    if ('error' in result) {
      this.ruleErrors.update((errors) => ({ ...errors, [group.key]: result.error }));
      return;
    }

    const exists = this.rules().some(
      (r) => r.type === group.type && r.isExclusion === group.isExclusion && r.value.toLowerCase() === result.value.toLowerCase(),
    );
    if (!exists) {
      this.rules.update((rules) => [...rules, { type: group.type, value: result.value, isExclusion: group.isExclusion }]);
    }

    this.ruleErrors.update(({ [group.key]: _, ...rest }) => rest);
    event.chipInput.clear();
  }

  protected removeRule(rule: RuleInput): void {
    this.rules.update((rules) => rules.filter((r) => r !== rule));
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.hasInclusion() || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.serverRuleErrors.set([]);
    const { name, description, priority } = this.form.getRawValue();
    const request = { name: name.trim(), description: description.trim() || null, priority, rules: this.rules() };
    const id = this.id();

    const save$ = id
      ? this.topicsService.update(id, { ...request, version: this.version })
      : this.topicsService.create(request);

    save$.subscribe({
      next: (topic) => {
        this.notifier.success(id ? 'Topic updated.' : 'Topic created. The first collection has been queued.');
        void this.router.navigate(['/topics', topic.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.applyServerErrors(error);
      },
    });
  }

  private applyServerErrors(error: unknown): void {
    const apiError = toApiError(error);
    const entries = Object.entries(apiError.fieldErrors);

    if (entries.length === 0) {
      this.notifier.error(apiError.message);
      return;
    }

    const ruleMessages: string[] = [];
    for (const [field, messages] of entries) {
      const control = this.form.get(field);
      if (control) {
        control.setErrors({ server: messages[0] });
      } else {
        ruleMessages.push(...messages);
      }
    }
    this.serverRuleErrors.set(ruleMessages);
  }
}
