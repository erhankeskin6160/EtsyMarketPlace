import { extractEtsyListingRef } from './etsy-listing-url.util';

describe('extractEtsyListingRef', () => {
  it('should extract listingId and preserve URL for standard Etsy listing URLs', () => {
    const url = 'https://www.etsy.com/listing/1823456789/sauron-dark-lord-statue?ref=sr_gallery';
    const res = extractEtsyListingRef(url);
    expect(res).toEqual({
      listingId: 1823456789,
      listingUrl: url
    });
  });

  it('should extract listingId for localized Etsy URLs', () => {
    const url = 'https://www.etsy.com/uk/listing/1523495029/sauron-statue';
    const res = extractEtsyListingRef(url);
    expect(res).toEqual({
      listingId: 1523495029,
      listingUrl: url
    });
  });

  it('should build canonical Etsy URL when given a raw numeric listing ID', () => {
    const res = extractEtsyListingRef('4431044510');
    expect(res).toEqual({
      listingId: 4431044510,
      listingUrl: 'https://www.etsy.com/listing/4431044510'
    });
  });

  it('should return null for empty or non-Etsy inputs', () => {
    expect(extractEtsyListingRef('')).toBeNull();
    expect(extractEtsyListingRef('https://example.com/listing/12345678')).toBeNull();
    expect(extractEtsyListingRef('random text')).toBeNull();
  });
});
