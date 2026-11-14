import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { validationErrorInterceptor } from './validation-error.interceptor';

describe('validationErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastrSpy: jasmine.SpyObj<ToastrService>;

  beforeEach(() => {
    toastrSpy = jasmine.createSpyObj<ToastrService>('ToastrService', ['warning', 'error']);

    TestBed.configureTestingModule({
      providers: [
        { provide: ToastrService, useValue: toastrSpy },
        provideHttpClient(withInterceptors([validationErrorInterceptor])),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows ngx-toastr warning and sanitizes message on 400 FluentValidation response', () => {
    let capturedError: any;

    http.post('/api/etsy/listings', {}).subscribe({
      error: err => (capturedError = err)
    });

    const req = httpMock.expectOne('/api/etsy/listings');
    req.flush(
      {
        success: false,
        statusCode: 400,
        message: 'Validasyon kuralları doğrulanamadı.',
        errors: {
          ShippingProfileId: ["Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili gereklidir."]
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(toastrSpy.warning).toHaveBeenCalledTimes(1);
    expect(capturedError?.error?.message).toContain('Kargo Profili');
    expect(capturedError?.message).not.toContain('Http failure response');
  });

  it('shows ngx-toastr error without leaking raw system error on 500 failure', () => {
    let capturedError: any;

    http.post('/api/etsy/accounting/expenses', {}).subscribe({
      error: err => (capturedError = err)
    });

    const req = httpMock.expectOne('/api/etsy/accounting/expenses');
    req.flush(
      { success: false, message: 'Sunucu hatası: System.NullReferenceException' },
      { status: 500, statusText: 'Internal Server Error' }
    );

    expect(toastrSpy.error).toHaveBeenCalledTimes(1);
    expect(capturedError?.error?.message).toContain('Sunucu tarafında geçici bir işlem hatası');
    expect(capturedError?.message).not.toContain('Http failure response');
  });
});
