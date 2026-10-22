import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Rasid brand mark: radar arcs (observation) and a gold dot (a detected signal). */
@Component({
  selector: 'rasid-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 64 64" aria-hidden="true">
      <defs>
        <linearGradient [attr.id]="gradientId" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#0b6f68" />
          <stop offset="0.5" stop-color="#0f8f86" />
          <stop offset="1" stop-color="#1aa6b7" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="16" [attr.fill]="'url(#' + gradientId + ')'" />
      <path d="M20 28a16 16 0 0 1 16 16" fill="none" stroke="#fff" stroke-width="5" stroke-linecap="round" />
      <path d="M20 15a29 29 0 0 1 29 29" fill="none" stroke="#fff" stroke-opacity="0.55" stroke-width="5" stroke-linecap="round" />
      <circle cx="20" cy="44" r="5.5" fill="#fff" />
      <circle cx="47" cy="17" r="4.5" fill="#d9a23b" />
    </svg>
    @if (showName()) {
      <span class="wordmark">
        <span class="name">Rasid</span>
        <span class="arabic" lang="ar">رصد</span>
      </span>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 10px; }
    svg { flex: none; filter: drop-shadow(0 4px 10px rgb(15 143 134 / 30%)); }
    .wordmark { display: flex; align-items: baseline; gap: 8px; line-height: 1; }
    .name { font-weight: 800; font-size: 1.25rem; letter-spacing: -0.03em; color: var(--r-text); }
    .arabic { font-family: 'Noto Kufi Arabic', var(--r-font); font-weight: 700; font-size: 0.95rem; color: var(--r-brand); }
  `,
})
export class Logo {
  readonly size = input(36);
  readonly showName = input(true);

  private static nextId = 0;
  protected readonly gradientId = `rasid-logo-${Logo.nextId++}`;
}
