export interface ParsedEtsyListingRef {
  listingId: number;
  listingUrl: string;
}

/**
 * Extracts a numeric Etsy listing ID and normalized listing URL from either a full
 * Etsy listing URL (including localized paths like /uk/listing/...) or a raw numeric ID.
 */
export function extractEtsyListingRef(raw: string | null | undefined): ParsedEtsyListingRef | null {
  const input = (raw || '').trim();
  if (!input) return null;

  if (/^\d{5,15}$/.test(input)) {
    const listingId = Number(input);
    if (listingId > 0) {
      return {
        listingId,
        listingUrl: `https://www.etsy.com/listing/${listingId}`
      };
    }
  }

  const urlMatch = input.match(/https?:\/\/(?:www\.)?etsy\.com\/(?:[a-z]{2}(?:-[a-z]{2})?\/)?listing\/(\d{5,15})/i);
  if (urlMatch && urlMatch[1]) {
    const listingId = Number(urlMatch[1]);
    if (listingId > 0) {
      return {
        listingId,
        listingUrl: input
      };
    }
  }

  return null;
}
