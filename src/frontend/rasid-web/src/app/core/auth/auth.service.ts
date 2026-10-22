import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { ApiEndpoints } from '../config/api-endpoints';
import { SignInResponse, User } from '../models/api.models';

export const Roles = {
  admin: 'Admin',
  analyst: 'Analyst',
  viewer: 'Viewer',
} as const;

/**
 * Session management. The access token lives only in memory (never in localStorage);
 * the refresh token is an HttpOnly cookie the browser sends to the auth endpoints, so a reload
 * restores the session without exposing any token to JavaScript.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  private readonly accessToken = signal<string | null>(null);
  private refreshInFlight: Observable<boolean> | null = null;

  readonly user = signal<User | null>(null);
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isAdmin = computed(() => this.hasAnyRole(Roles.admin));
  readonly canManageTopics = computed(() => this.hasAnyRole(Roles.admin, Roles.analyst));

  token(): string | null {
    return this.accessToken();
  }

  signIn(email: string, password: string): Observable<User> {
    return this.api
      .post<SignInResponse>(ApiEndpoints.auth.login, { email, password }, true)
      .pipe(
        tap((response) => this.setSession(response)),
        map((response) => response.user),
      );
  }

  /** Exchanges the refresh cookie for a new access token. Concurrent callers share one request. */
  refresh(): Observable<boolean> {
    this.refreshInFlight ??= this.api.post<SignInResponse>(ApiEndpoints.auth.refresh, {}, true).pipe(
      tap((response) => this.setSession(response)),
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /**
   * Called once at start-up to restore a session from the refresh cookie. Skipped when this
   * browser has never signed in (no hint), avoiding a pointless 401 on a first visit.
   */
  restoreSession(): Observable<boolean> {
    return readSessionHint() ? this.refresh() : of(false);
  }

  signOut(): void {
    this.api
      .post<void>(ApiEndpoints.auth.logout, {}, true)
      .pipe(catchError(() => of(undefined)))
      .subscribe(() => {
        this.clearSession();
        void this.router.navigate(['/login']);
      });
  }

  /** The session could not be renewed: send the user to sign-in, keeping where they were. */
  sessionExpired(): void {
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url, expired: 1 } });
  }

  hasAnyRole(...roles: string[]): boolean {
    const userRoles = this.user()?.roles ?? [];
    return roles.some((role) => userRoles.includes(role));
  }

  private setSession(response: SignInResponse): void {
    this.accessToken.set(response.accessToken);
    this.user.set(response.user);
    writeSessionHint(true);
  }

  private clearSession(): void {
    this.accessToken.set(null);
    this.user.set(null);
    writeSessionHint(false);
  }
}

/** A non-sensitive flag ("this browser may hold a refresh cookie"). No token is ever stored. */
const SessionHintKey = 'rasid.session';

function readSessionHint(): boolean {
  try {
    return localStorage.getItem(SessionHintKey) === '1';
  } catch {
    return true; // Storage unavailable: attempt the restore anyway.
  }
}

function writeSessionHint(present: boolean): void {
  try {
    if (present) {
      localStorage.setItem(SessionHintKey, '1');
    } else {
      localStorage.removeItem(SessionHintKey);
    }
  } catch {
    // Storage unavailable: restore will simply be attempted on every start.
  }
}
