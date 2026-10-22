import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Post } from '../../core/models/api.models';
import { CompactNumberPipe, LocalDateTimePipe, RatePipe, RelativeTimePipe } from '../formatting/format.pipes';
import { LevelChip } from './chips';

/**
 * One public post, displayed per X's display requirements: author name, @username, time,
 * unaltered text and a link to the original. Sample posts are labelled and have no link.
 */
@Component({
  selector: 'rasid-post-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CompactNumberPipe, RatePipe, RelativeTimePipe, LocalDateTimePipe, LevelChip],
  template: `
    @let p = post();
    <article class="post">
      <header class="post__head">
        @if (p.author.profileImageUrl) {
          <img class="post__avatar" [src]="p.author.profileImageUrl" alt="" width="40" height="40" loading="lazy" referrerpolicy="no-referrer" />
        } @else {
          <span class="post__avatar post__avatar--initials" aria-hidden="true">{{ initials() }}</span>
        }
        <div class="post__who">
          <span class="post__name">
            {{ p.author.displayName }}
            @if (p.author.isVerified) {
              <span class="material-symbols-rounded post__verified" aria-label="Verified">verified</span>
            }
          </span>
          <span class="post__handle">&#64;{{ p.author.username }} · <time [attr.datetime]="p.createdAtUtc" [title]="p.createdAtUtc | localDateTime">{{ p.createdAtUtc | relativeTime }}</time></span>
        </div>
        <rasid-level-chip [level]="p.engagementLevel" />
      </header>

      <div class="post__body" [class.post__body--media]="p.media">
        <p class="post__text" dir="auto" [attr.lang]="p.language">{{ p.text }}</p>
        @if (p.media; as m) {
          <figure class="post__media">
            <img [src]="m.previewUrl" [alt]="m.altText ?? 'Image attached to the post'" loading="lazy" decoding="async" referrerpolicy="no-referrer" />
            @if (p.source === 'Sample') {
              <figcaption class="post__media-badge">Sample image</figcaption>
            }
            @if (m.kind !== 'photo') {
              <span class="post__media-kind material-symbols-rounded" aria-hidden="true">play_circle</span>
            }
          </figure>
        }
      </div>

      <footer class="post__metrics">
        <span title="Likes"><span class="material-symbols-rounded" aria-hidden="true">favorite</span>{{ p.metrics.likes | compact }}</span>
        <span title="Reposts"><span class="material-symbols-rounded" aria-hidden="true">repeat</span>{{ p.metrics.reposts | compact }}</span>
        <span title="Replies"><span class="material-symbols-rounded" aria-hidden="true">chat_bubble</span>{{ p.metrics.replies | compact }}</span>
        <span title="Quotes"><span class="material-symbols-rounded" aria-hidden="true">format_quote</span>{{ p.metrics.quotes | compact }}</span>
        <span title="Views"><span class="material-symbols-rounded" aria-hidden="true">visibility</span>{{ p.metrics.views | compact }}</span>
        <span class="post__rate" title="Engagement rate">{{ p.engagementRate | rate }}</span>
        @if (p.url) {
          <a class="post__link" [href]="p.url" target="_blank" rel="noopener noreferrer">
            View on X <span class="material-symbols-rounded" aria-hidden="true">open_in_new</span>
          </a>
        } @else {
          <span class="post__link post__link--sample" title="Fictitious sample post — there is no original on X">Sample post</span>
        }
      </footer>
    </article>
  `,
  styles: `
    :host { display: block; }
    .post { padding: 16px 0; display: grid; gap: 10px; }
    .post__head { display: flex; align-items: center; gap: 12px; }
    .post__avatar { width: 40px; height: 40px; border-radius: 50%; flex: none; object-fit: cover; }
    .post__avatar--initials {
      display: grid; place-items: center;
      background: var(--r-brand-soft); color: var(--r-brand);
      font-weight: 700; font-size: 0.85rem;
    }
    .post__who { flex: 1; min-width: 0; display: grid; }
    .post__name { font-weight: 650; display: flex; align-items: center; gap: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .post__verified { font-size: 16px; color: var(--r-info); }
    .post__handle { color: var(--r-text-muted); font-size: 0.82rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .post__text { margin: 0; line-height: 1.55; white-space: pre-line; overflow-wrap: anywhere; }
    .post__body { display: grid; gap: 12px; }
    @media (min-width: 720px) { .post__body--media { grid-template-columns: minmax(0, 1fr) 168px; align-items: start; } }
    .post__media { position: relative; margin: 0; border-radius: 12px; overflow: hidden; aspect-ratio: 16 / 9; background: var(--r-surface-2); }
    .post__media img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform 0.4s ease; }
    .post:hover .post__media img { transform: scale(1.04); }
    .post__media-badge { position: absolute; left: 6px; bottom: 6px; padding: 2px 8px; border-radius: 999px;
                         background: rgb(15 23 42 / 70%); color: #fff; font-size: 0.66rem; font-weight: 700; letter-spacing: 0.02em; }
    .post__media-kind { position: absolute; inset: 0; display: grid; place-items: center; font-size: 44px; color: #fff; text-shadow: 0 2px 8px rgb(0 0 0 / 45%); }
    .post__metrics {
      display: flex; flex-wrap: wrap; align-items: center; gap: 6px 16px;
      color: var(--r-text-muted); font-size: 0.82rem;
    }
    .post__metrics > span { display: inline-flex; align-items: center; gap: 4px; }
    .post__metrics .material-symbols-rounded { font-size: 17px; }
    .post__rate { font-weight: 700; color: var(--r-text); }
    .post__link { margin-left: auto; display: inline-flex; align-items: center; gap: 2px; font-weight: 600; text-decoration: none; }
    .post__link .material-symbols-rounded { font-size: 16px; }
    .post__link--sample { color: var(--r-warning); }
  `,
})
export class PostCard {
  readonly post = input.required<Post>();

  protected readonly initials = computed(() =>
    this.post()
      .author.displayName.split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join(''),
  );
}
