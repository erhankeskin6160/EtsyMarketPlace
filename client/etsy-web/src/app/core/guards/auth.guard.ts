import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { catchError, map, of } from 'rxjs';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.fetchCurrentUser().pipe(
    map(user => user !== null || router.createUrlTree(['/auth/login'])),
    catchError(() => of(router.createUrlTree(['/auth/login'])))
  );
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.fetchCurrentUser().pipe(
    map(user => user?.role === 'Admin' || router.createUrlTree(['/dashboard'])),
    catchError(() => of(router.createUrlTree(['/auth/login'])))
  );
};
