import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconButton } from '@angular/material/button';
import { MatDivider } from '@angular/material/divider';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTooltip } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { Alert } from '../core/models/api.models';
import { LiveStatusStore } from '../core/services/live-status.store';
import { ThemeService } from '../core/theme/theme.service';
import { RelativeTimePipe } from '../shared/formatting/format.pipes';
import { SourceBadge } from '../shared/ui/chips';
import { Logo } from '../shared/ui/logo';
import { AlertKindIcon } from '../shared/ui/status-display';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  adminOnly?: boolean;
}

const NavItems: NavItem[] = [
  { path: '/overview', label: 'Overview', icon: 'space_dashboard' },
  { path: '/topics', label: 'Topics', icon: 'radar' },
  { path: '/about', label: 'Method', icon: 'menu_book' },
  { path: '/system', label: 'System', icon: 'monitor_heart', adminOnly: true },
];

/**
 * Responsive application frame. Desktop: persistent sidebar. Phone: compact top bar with a
 * bottom navigation bar (thumb-reachable), mirroring a native mobile app.
 */
@Component({
  selector: 'rasid-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, Logo, MatIconButton, MatMenu, MatMenuItem, MatMenuTrigger, MatDivider, MatTooltip,
    SourceBadge, RelativeTimePipe,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly status = inject(LiveStatusStore);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly navItems = computed(() => NavItems.filter((item) => !item.adminOnly || this.auth.isAdmin()));

  protected readonly initials = computed(() =>
    (this.auth.user()?.displayName ?? '?')
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join(''),
  );

  ngOnInit(): void {
    this.status.start();
  }

  ngOnDestroy(): void {
    this.status.stop();
  }

  protected alertIcon(alert: Alert): string {
    return AlertKindIcon[alert.kind];
  }
}
