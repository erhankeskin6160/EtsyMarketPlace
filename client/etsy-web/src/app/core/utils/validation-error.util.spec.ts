import { HttpErrorResponse } from '@angular/common/http';
import { extractClientValidationErrors } from './validation-error.util';

describe('extractClientValidationErrors', () => {
  it('extracts FluentValidation dictionary errors and strips Command prefix', () => {
    const httpError = new HttpErrorResponse({
      status: 400,
      statusText: 'Bad Request',
      url: 'http://5.180.81.148:5263/api/etsy/listings',
      error: {
        success: false,
        statusCode: 400,
        message: 'Validasyon kuralları doğrulanamadı.',
        errors: {
          'Command.ShippingProfileId': [
            "Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili gereklidir."
          ],
          Title: ['Ürün başlığı zorunludur.']
        }
      }
    });

    const parsed = extractClientValidationErrors(httpError);

    expect(parsed.isValidationError).toBeTrue();
    expect(parsed.fieldErrors.length).toBe(2);
    expect(parsed.fieldErrors[0].field).toBe('ShippingProfileId');
    expect(parsed.fieldErrors[0].label).toBe('Kargo Profili');
    expect(parsed.fieldErrors[1].field).toBe('Title');
    expect(parsed.fieldErrors[1].label).toBe('Ürün Başlığı');
  });

  it('never leaks raw Angular Http failure response message to end users', () => {
    const httpError = new HttpErrorResponse({
      status: 500,
      statusText: 'Internal Server Error',
      url: 'http://5.180.81.148:5263/api/etsy/listings',
      error: null
    });

    const parsed = extractClientValidationErrors(httpError);

    expect(parsed.isValidationError).toBeFalse();
    expect(parsed.summary).not.toContain('Http failure response');
    expect(parsed.summary).toContain('Sunucu tarafında');
  });
});
