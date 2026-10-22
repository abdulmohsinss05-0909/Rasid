import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';

export type ThemePreference = 'system' | 'light' | 'dark';

const StorageKey = 'rasid.theme';
const ThemeColors = { light: '#f4f7f8', dark: '#0b1117' } as const;

/**
 * Light / dark / follow-system theme. The choice is a per-device convenience kept in localStorage;
 * the page works normally when storage is unavailable.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly systemPrefersDark = signal(this.media?.matches ?? false);

  readonly preference = signal<ThemePreference>(readPreference());
  readonly isDark = computed(
    () => this.preference() === 'dark' || (this.preference() === 'system' && this.systemPrefersDark()),
  );

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));

    effect(() => {
      const dark = this.isDark();
      const root = this.document.documentElement;
      root.classList.toggle('dark', dark);
      root.style.colorScheme = dark ? 'dark' : 'light';
      this.document
        .querySelector('meta[name="theme-color"]')
        ?.setAttribute('content', dark ? ThemeColors.dark : ThemeColors.light);
      writePreference(this.preference());
    });
  }

  toggle(): void {
    this.preference.set(this.isDark() ? 'light' : 'dark');
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
  }
}

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(StorageKey);
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  } catch {
    return 'system';
  }
}

function writePreference(preference: ThemePreference): void {
  try {
    localStorage.setItem(StorageKey, preference);
  } catch {
    // Storage unavailable (private mode or blocked): the theme still applies for this visit.
  }
}
