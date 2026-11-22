import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BreakpointService } from './breakpoint.service';

describe('BreakpointService', () => {
  let service: BreakpointService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])]
    });
    service = TestBed.inject(BreakpointService);
  });

  it('should compute responsive tiers accurately from viewportWidth', () => {
    service.viewportWidth.set(375);
    expect(service.currentTier()).toBe('xs');
    expect(service.isSmallPhone()).toBeTrue();
    expect(service.isMobile()).toBeTrue();
    expect(service.isCompactLayout()).toBeTrue();
    expect(service.isDesktop()).toBeFalse();

    service.viewportWidth.set(700);
    expect(service.currentTier()).toBe('sm');
    expect(service.isSmallPhone()).toBeFalse();
    expect(service.isMobile()).toBeTrue();

    service.viewportWidth.set(820);
    expect(service.currentTier()).toBe('md');
    expect(service.isMobile()).toBeFalse();
    expect(service.isTablet()).toBeTrue();
    expect(service.isCompactLayout()).toBeTrue();

    service.viewportWidth.set(1280);
    expect(service.currentTier()).toBe('xl');
    expect(service.isDesktop()).toBeTrue();
    expect(service.isCompactLayout()).toBeFalse();
  });

  it('should toggle, open and close mobile drawer state', () => {
    expect(service.isDrawerOpen()).toBeFalse();

    service.openDrawer();
    expect(service.isDrawerOpen()).toBeTrue();

    service.toggleDrawer();
    expect(service.isDrawerOpen()).toBeFalse();

    service.toggleDrawer();
    expect(service.isDrawerOpen()).toBeTrue();

    service.closeDrawer();
    expect(service.isDrawerOpen()).toBeFalse();
  });
});
