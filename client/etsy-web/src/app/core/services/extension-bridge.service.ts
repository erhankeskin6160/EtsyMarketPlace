import { Injectable } from '@angular/core';
import { CompetitorListingInventoryDto } from './etsy-api.service';

@Injectable({
  providedIn: 'root'
})
export class ExtensionBridgeService {
  private bridgeDetected = false;

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('message', (event: MessageEvent) => {
        if (event.source === window && event.data?.type === 'ETSY_COPILOT_BRIDGE_READY') {
          this.bridgeDetected = true;
        }
      });
      window.postMessage({ type: 'ETSY_COPILOT_BRIDGE_PING' }, '*');
    }
  }

  async checkBridgeAvailable(waitMs = 300): Promise<boolean> {
    if (typeof window === 'undefined') return false;
    if (this.bridgeDetected) return true;

    return new Promise<boolean>((resolve) => {
      let settled = false;
      const done = (ok: boolean) => {
        if (settled) return;
        settled = true;
        window.removeEventListener('message', onMsg);
        clearTimeout(timer);
        resolve(ok);
      };

      const onMsg = (event: MessageEvent) => {
        if (event.source === window && event.data?.type === 'ETSY_COPILOT_BRIDGE_READY') {
          this.bridgeDetected = true;
          done(true);
        }
      };

      const timer = setTimeout(() => done(false), waitMs);
      window.addEventListener('message', onMsg);
      window.postMessage({ type: 'ETSY_COPILOT_BRIDGE_PING' }, '*');
    });
  }

  async fetchListingVariationsViaExtension(
    listingId: number,
    listingUrl: string,
    timeoutMs = 11000
  ): Promise<CompetitorListingInventoryDto | null> {
    if (typeof window === 'undefined' || !listingId) return null;

    const available = await this.checkBridgeAvailable(300);
    if (!available) return null;

    const requestId = `var_${listingId}_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;

    return new Promise<CompetitorListingInventoryDto | null>((resolve) => {
      let settled = false;
      const finish = (res: CompetitorListingInventoryDto | null) => {
        if (settled) return;
        settled = true;
        window.removeEventListener('message', onResponse);
        clearTimeout(timer);
        resolve(res);
      };

      const onResponse = (event: MessageEvent) => {
        if (event.source !== window || !event.data || event.data.type !== 'ETSY_COPILOT_FETCH_VARIATIONS_RES') {
          return;
        }
        if (event.data.requestId !== requestId) return;

        if (event.data.ok && event.data.inventory?.hasVariations) {
          finish(event.data.inventory as CompetitorListingInventoryDto);
        } else {
          finish(null);
        }
      };

      const timer = setTimeout(() => finish(null), timeoutMs);
      window.addEventListener('message', onResponse);
      window.postMessage({
        type: 'ETSY_COPILOT_FETCH_VARIATIONS_REQ',
        requestId,
        listingId,
        listingUrl
      }, '*');
    });
  }
}
