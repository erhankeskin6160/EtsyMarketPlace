import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SubscriptionService } from '../services/subscription.service';

export function subscriptionFeatureGuard(featureKey: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const subscription = inject(SubscriptionService);
    const router = inject(Router);

    if (auth.isAdmin()) {
      return true;
    }

    return subscription.ensureSubscriptionLoaded().pipe(
      map(() => {
        if (subscription.isFeatureAllowed(featureKey)) {
          return true;
        }
        return router.createUrlTree(['/settings/subscription'], {
          queryParams: { upgrade: featureKey }
        });
      }),
      catchError(() => of(true))
    );
  };
}
