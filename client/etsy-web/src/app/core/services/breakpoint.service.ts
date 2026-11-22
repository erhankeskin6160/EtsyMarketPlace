import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

export type ViewportTier = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

/**
 * Reactive viewport and mobile navigation drawer state service powered by Angular Signals.
 * Standard breakpoints:
 * - xs: < 640px (small phones)
 * - sm: 640px - 767.98px (large phones)
 * - md: 768px - 1023.98px (tablets)
 * - lg: 1024px - 1279.98px (desktops)
 * - xl: >= 1280px (wide desktops)
 */
@Injectable({ providedIn: 'root' })
export class BreakpointService {
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  readonly viewportWidth = signal<number>(typeof window !== 'undefined' ? window.innerWidth : 1280);
  readonly isDrawerOpen = signal<boolean>(false);

  readonly currentTier = computed<ViewportTier>(() => {
    const w = this.viewportWidth();
    if (w < 640) return 'xs';
    if (w < 768) return 'sm';
    if (w < 1024) return 'md';
    if (w < 1280) return 'lg';
    return 'xl';
  });

  readonly isSmallPhone = computed(() => this.viewportWidth() < 640);
  readonly isMobile = computed(() => this.viewportWidth() < 768);
  readonly isTablet = computed(() => {
    const w = this.viewportWidth();
    return w >= 768 && w < 1024;
  });
  readonly isCompactLayout = computed(() => this.viewportWidth() < 1024);
  readonly isDesktop = computed(() => this.viewportWidth() >= 1024);

  constructor() {
    if (typeof window !== 'undefined') {
      const onResize = () => {
        const width = window.innerWidth;
        this.viewportWidth.set(width);
        if (width >= 1024 && this.isDrawerOpen()) {
          this.isDrawerOpen.set(false);
        }
      };

      window.addEventListener('resize', onResize, { passive: true });
      this.destroyRef.onDestroy(() => window.removeEventListener('resize', onResize));
    }

    // Automatically close mobile drawer on route navigation
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        if (this.isDrawerOpen()) {
          this.isDrawerOpen.set(false);
        }
      });
  }

  openDrawer(): void {
    this.isDrawerOpen.set(true);
  }

  closeDrawer(): void {
    this.isDrawerOpen.set(false);
  }

  toggleDrawer(): void {
    this.isDrawerOpen.update((open) => !open);
  }
}
