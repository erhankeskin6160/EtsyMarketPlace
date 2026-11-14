import { HttpErrorResponse } from '@angular/common/http';

export interface FieldValidationError {
  field: string;
  label: string;
  messages: string[];
}

export interface ParsedClientError {
  summary: string;
  fieldErrors: FieldValidationError[];
  isValidationError: boolean;
}

const DEFAULT_FIELD_LABELS: Record<string, string> = {
  shopid: 'Mağaza',
  title: 'Ürün Başlığı',
  description: 'Açıklama',
  price: 'Fiyat',
  quantity: 'Stok Adedi',
  taxonomyid: 'Kategori',
  shippingprofileid: 'Kargo Profili',
  readinessstateid: 'Hazırlık Süresi',
  tags: 'Etiketler',
  materials: 'Materyaller',
  variationgroups: 'Varyasyonlar',
  variations: 'Varyasyonlar',
  state: 'İlan Durumu',
  listingid: 'İlan Kimliği',
  alttext: 'Görsel Alt-Text',
  items: 'Görseller / Öğeler',
  amount: 'Tutar',
  entrydate: 'Belge Tarihi',
  assetname: 'Varlık Adı',
  acquisitiondate: 'Alış Tarihi',
  acquisitioncosttry: 'Alış Bedeli',
  usefullifeyears: 'Ekonomik Ömür',
  salvagevaluetry: 'Hurda Değer',
  eexportexemptionratepct: 'E-İhracat İstisna Oranı',
  customorcorporatetaxratepct: 'Vergi Oranı',
  year: 'Yıl',
  month: 'Ay',
  keyword: 'Anahtar Kelime',
  limit: 'Limit',
  shopnameorurl: 'Mağaza Adı / URL',
  accesstoken: 'Access Token',
  refreshtoken: 'Refresh Token',
  accesstokenexpiresat: 'Token Bitiş Tarihi',
  code: 'Yetki Kodu',
  settingsjson: 'Ayar Verisi',
  chatid: 'Telegram Chat ID',
  carrierid: 'Kargo Taşıyıcı',
  records: 'Sipariş Kayıtları',
  orders: 'Sipariş Listesi',
  receiptid: 'Sipariş No',
  weightkg: 'Ağırlık (kg)',
  reservepercent: 'Rezerv Oranı',
  startdate: 'Başlangıç Tarihi',
  enddate: 'Bitiş Tarihi',
  overallscore: 'Kalite Puanı',
  id: 'Kayıt Kimliği',
  email: 'E-posta',
  username: 'Kullanıcı Adı',
  password: 'Şifre',
  role: 'Rol',
  monthlyaitokenquota: 'AI Token Kotası',
  countrycode: 'Ülke Kodu',
  postalcode: 'Posta Kodu',
  carrier: 'Taşıyıcı',
  general: 'Genel'
};

const WRAPPER_PREFIX_REGEX = /^(command|request|dto|experiment|capture|query|snapshot|record)\./i;

function normalizeFieldKey(rawKey: string): string {
  const cleaned = rawKey.replace(WRAPPER_PREFIX_REGEX, '').trim();
  const base = cleaned.split('[')[0].split('.')[0];
  return base || 'General';
}

function resolveFieldLabel(field: string, customLabels?: Record<string, string>): string {
  const lower = field.toLowerCase();
  if (customLabels) {
    const customMatch = Object.entries(customLabels).find(([k]) => k.toLowerCase() === lower);
    if (customMatch) return customMatch[1];
  }
  return DEFAULT_FIELD_LABELS[lower] || field;
}

function isRawHttpSystemMessage(msg: string | undefined | null): boolean {
  if (!msg) return true;
  const trimmed = msg.trim();
  return (
    trimmed.startsWith('Http failure response for ') ||
    trimmed.startsWith('Http failure during parsing ') ||
    /^Unknown Error$/i.test(trimmed)
  );
}

/**
 * Backend FluentValidation (ValidationErrorResponse) ve ServiceResponse hatalarını
 * son kullanıcıya gösterilebilir yapısal alan hatalarına dönüştürür;
 * ham Angular HTTP / sistem hata metinlerini asla sızdırmaz.
 */
export function extractClientValidationErrors(
  err: unknown,
  fallbackMessage = 'İşlem sırasında bir hata oluştu. Lütfen girdiğiniz bilgileri kontrol edip tekrar deneyin.',
  customFieldLabels?: Record<string, string>
): ParsedClientError {
  const httpErr = err instanceof HttpErrorResponse ? err : (err as any);
  const body = httpErr?.error;
  const fieldMap = new Map<string, { field: string; label: string; messages: Set<string> }>();

  const addError = (rawField: string, msg: string) => {
    const cleanMsg = msg?.trim();
    if (!cleanMsg) return;
    const field = normalizeFieldKey(rawField);
    const key = field.toLowerCase();
    if (!fieldMap.has(key)) {
      fieldMap.set(key, {
        field,
        label: resolveFieldLabel(field, customFieldLabels),
        messages: new Set<string>()
      });
    }
    fieldMap.get(key)!.messages.add(cleanMsg);
  };

  if (body && typeof body === 'object') {
    const rawErrors = (body as any).errors ?? (body as any).Errors;
    if (Array.isArray(rawErrors)) {
      for (const item of rawErrors) {
        if (typeof item === 'string') {
          addError('General', item);
        }
      }
    } else if (rawErrors && typeof rawErrors === 'object') {
      for (const [rawKey, val] of Object.entries(rawErrors)) {
        if (Array.isArray(val)) {
          for (const msg of val) {
            if (typeof msg === 'string') addError(rawKey, msg);
          }
        } else if (typeof val === 'string') {
          addError(rawKey, val);
        }
      }
    }
  }

  const fieldErrors: FieldValidationError[] = Array.from(fieldMap.values()).map(entry => ({
    field: entry.field,
    label: entry.label,
    messages: Array.from(entry.messages)
  }));

  const bodyMessage =
    typeof body === 'string' && !isRawHttpSystemMessage(body)
      ? body.trim()
      : typeof (body as any)?.message === 'string' && !isRawHttpSystemMessage((body as any).message)
        ? (body as any).message.trim()
        : typeof (body as any)?.error === 'string' && !isRawHttpSystemMessage((body as any).error)
          ? (body as any).error.trim()
          : '';

  if (fieldErrors.length > 0) {
    const summary =
      bodyMessage && bodyMessage !== 'Validasyon kuralları doğrulanamadı.'
        ? bodyMessage
        : fieldErrors.length === 1 && fieldErrors[0].messages.length === 1
          ? fieldErrors[0].messages[0]
          : 'Lütfen formdaki doğrulama uyarılarını düzeltin.';

    return {
      summary,
      fieldErrors,
      isValidationError: true
    };
  }

  const status = typeof httpErr?.status === 'number' ? httpErr.status : 0;
  if (status === 0) {
    return {
      summary: 'Sunucuya bağlanılamadı. Lütfen internet veya VDS bağlantınızı kontrol edin.',
      fieldErrors: [],
      isValidationError: false
    };
  }

  if (status >= 500) {
    return {
      summary: bodyMessage && !bodyMessage.startsWith('Sunucu hatası:')
        ? bodyMessage
        : 'Sunucu tarafında geçici bir işlem hatası oluştu. Lütfen biraz sonra tekrar deneyin.',
      fieldErrors: [],
      isValidationError: false
    };
  }

  return {
    summary: bodyMessage || fallbackMessage,
    fieldErrors: [],
    isValidationError: status === 400
  };
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * ngx-toastr (enableHtml: true) için güvenli, alan-bazlı HTML uyarı içeriği oluşturur.
 */
export function formatValidationToastHtml(parsed: ParsedClientError): string {
  if (parsed.fieldErrors.length === 0) {
    return escapeHtml(parsed.summary);
  }
  if (parsed.fieldErrors.length === 1 && parsed.fieldErrors[0].messages.length === 1) {
    const item = parsed.fieldErrors[0];
    return `<strong>${escapeHtml(item.label)}:</strong> ${escapeHtml(item.messages[0])}`;
  }

  const rows = parsed.fieldErrors
    .map(item => `<li><strong>${escapeHtml(item.label)}:</strong> ${escapeHtml(item.messages.join(' '))}</li>`)
    .join('');

  return `${escapeHtml(parsed.summary)}<ul>${rows}</ul>`;
}
