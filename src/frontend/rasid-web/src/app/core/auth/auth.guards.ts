import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Requires a signed-in user. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** Keeps signed-in users away from the sign-in page. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/overview']) : true;

/** Requires one of the given roles. A UI convenience only: the API enforces the same rule. */
export function roleGuard(...roles: string[]): CanActivateFn {
  return () => (inject(AuthService).hasAnyRole(...roles) ? true : inject(Router).createUrlTree(['/topics']));
}
