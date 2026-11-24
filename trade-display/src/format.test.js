import { describe, expect, it } from 'vitest';
import { formatDateTime, formatPercent, formatPrice, formatQuantity, formatTime, priceDecimals, splitSymbol, timeZoneLabel } from './format.js';

describe('dates and times', () => {
    // 25/11/2026 01:02:03.456 UTC is 25/11/2026 14:02:03.456 in Auckland (NZDT, UTC+13).
    const summer = new Date('2026-11-25T01:02:03.456Z');
    // 15/07/2026 01:02:03 UTC is 15/07/2026 13:02:03 in Auckland (NZST, UTC+12).
    const winter = new Date('2026-07-15T01:02:03.000Z');

    it('formats trade times in Auckland time with milliseconds', () => {
        expect(formatTime(summer)).toBe('14:02:03.456');
    });

    it('formats dates as DD/MM/YYYY in Auckland time', () => {
        expect(formatDateTime(summer)).toBe('25/11/2026 14:02:03');
        expect(formatDateTime(winter)).toBe('15/07/2026 13:02:03');
    });

    it('rolls the date over at Auckland midnight, not UTC midnight', () => {
        expect(formatDateTime(new Date('2026-11-25T11:30:00Z'))).toBe('26/11/2026 00:30:00');
    });

    it('labels daylight saving correctly', () => {
        expect(timeZoneLabel(summer)).toBe('NZDT');
        expect(timeZoneLabel(winter)).toBe('NZST');
    });
});

describe('numbers', () => {
    it('chooses decimals by price magnitude', () => {
        expect(priceDecimals(95123.4)).toBe(2);
        expect(priceDecimals(180.12)).toBe(3);
        expect(priceDecimals(1.5)).toBe(4);
        expect(priceDecimals(0.00012)).toBe(6);
    });

    it('formats prices with grouping and fixed decimals', () => {
        expect(formatPrice(95123.4)).toBe('95,123.40');
        expect(formatPrice(0.5)).toBe('0.500000');
    });

    it('trims quantities to at most 6 decimals', () => {
        expect(formatQuantity(0.00125)).toBe('0.00125');
        expect(formatQuantity(1.123456789)).toBe('1.123457');
    });

    it('signs percentages', () => {
        expect(formatPercent(1.234)).toBe('+1.23%');
        expect(formatPercent(-0.5)).toBe('-0.50%');
        expect(formatPercent(0)).toBe('0.00%');
        expect(formatPercent(Number.NaN)).toBe('0.00%');
    });
});

describe('splitSymbol', () => {
    it.each([
        ['BTCUSDT', 'BTC', 'USDT'],
        ['ETHBTC', 'ETH', 'BTC'],
        ['SOLFDUSD', 'SOL', 'FDUSD'],
        ['NZD/USD', 'NZD', 'USD'],
        ['BTC-USD', 'BTC', 'USD'],
        ['XYZ', 'XYZ', ''],
    ])('%s -> %s / %s', (symbol, base, quote) => {
        expect(splitSymbol(symbol)).toEqual({ base, quote });
    });
});
