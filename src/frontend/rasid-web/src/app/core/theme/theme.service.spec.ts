import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('applies the dark theme to the document and remembers the choice', () => {
    const theme = TestBed.inject(ThemeService);

    theme.set('dark');
    TestBed.tick();

    expect(theme.isDark()).toBe(true);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('rasid.theme')).toBe('dark');
  });

  it('toggles between light and dark', () => {
    const theme = TestBed.inject(ThemeService);
    theme.set('light');
    theme.toggle();
    TestBed.tick();

    expect(theme.preference()).toBe('dark');
    theme.toggle();
    expect(theme.preference()).toBe('light');
  });
});
