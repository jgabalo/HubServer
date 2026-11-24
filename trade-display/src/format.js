// Display formatting. All times are shown in New Zealand time (Pacific/Auckland),
// dates as DD/MM/YYYY, numbers with en-NZ grouping.

const LOCALE = 'en-NZ';
export const TIME_ZONE = 'Pacific/Auckland';

const timeFormat = new Intl.DateTimeFormat(LOCALE, {
    timeZone: TIME_ZONE,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    fractionalSecondDigits: 3,
    hourCycle: 'h23',
});

const dateParts = new Intl.DateTimeFormat(LOCALE, {
    timeZone: TIME_ZONE,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
});

const zoneName = new Intl.DateTimeFormat(LOCALE, { timeZone: TIME_ZONE, timeZoneName: 'short' });

const numberFormats = new Map();

function numberFormat(minDigits, maxDigits) {
    const key = `${minDigits}:${maxDigits}`;
    let format = numberFormats.get(key);
    if (!format) {
        format = new Intl.NumberFormat(LOCALE, {
            minimumFractionDigits: minDigits,
            maximumFractionDigits: maxDigits,
        });
        numberFormats.set(key, format);
    }
    return format;
}

/** HH:mm:ss.SSS in Auckland time. */
export function formatTime(date) {
    return timeFormat.format(date);
}

/** DD/MM/YYYY HH:mm:ss in Auckland time. */
export function formatDateTime(date) {
    const parts = Object.fromEntries(dateParts.formatToParts(date).map(p => [p.type, p.value]));
    return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}:${parts.second}`;
}

/** Short zone label such as NZDT or NZST for the given instant. */
export function timeZoneLabel(date) {
    return zoneName.formatToParts(date).find(p => p.type === 'timeZoneName')?.value ?? 'NZT';
}

/** Decimal places that suit a price's magnitude, so BTC and low-priced coins both read well. */
export function priceDecimals(price) {
    const abs = Math.abs(price);
    if (abs >= 1000) return 2;
    if (abs >= 10) return 3;
    if (abs >= 1) return 4;
    return 6;
}

export function formatPrice(price) {
    const digits = priceDecimals(price);
    return numberFormat(digits, digits).format(price);
}

export function formatQuantity(quantity) {
    return numberFormat(0, 6).format(quantity);
}

export function formatNotional(value) {
    return numberFormat(2, 2).format(value);
}

/** Compact volume such as 1.2K or 3.4M. */
export function formatCompact(value) {
    return new Intl.NumberFormat(LOCALE, { notation: 'compact', maximumFractionDigits: 2 }).format(value);
}

export function formatPercent(value) {
    if (!Number.isFinite(value)) return '0.00%';
    const sign = value > 0 ? '+' : value < 0 ? '-' : '';
    return `${sign}${numberFormat(2, 2).format(Math.abs(value))}%`;
}

const QUOTE_ASSETS = ['FDUSD', 'USDT', 'USDC', 'TUSD', 'BUSD', 'USD', 'NZD', 'AUD', 'EUR', 'GBP', 'BTC', 'ETH', 'BNB'];

/** Splits an exchange symbol such as BTCUSDT into { base: 'BTC', quote: 'USDT' }. */
export function splitSymbol(symbol) {
    for (const separator of ['/', '-', '_']) {
        if (symbol.includes(separator)) {
            const [base, quote] = symbol.split(separator);
            return { base, quote };
        }
    }
    const quote = QUOTE_ASSETS.find(q => symbol.endsWith(q) && symbol.length > q.length);
    return quote ? { base: symbol.slice(0, -quote.length), quote } : { base: symbol, quote: '' };
}
