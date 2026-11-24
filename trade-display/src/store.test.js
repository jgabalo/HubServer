import { describe, expect, it } from 'vitest';
import { createTradeStore, sessionChange, tickDirection } from './store.js';

let nextId = 1;
function trade(overrides = {}) {
    return {
        source: 'Test',
        symbol: 'BTCUSDT',
        tradeId: String(nextId++),
        price: 100,
        quantity: 1,
        side: 'Buy',
        timestamp: new Date(Date.UTC(2026, 10, 25, 1, 0, nextId)).toISOString(),
        ...overrides,
    };
}

describe('createTradeStore', () => {
    it('keeps the tape newest first and capped', () => {
        const store = createTradeStore({ maxTape: 3 });
        const trades = [1, 2, 3, 4, 5].map(i => trade({ tradeId: `t${i}`, timestamp: `2026-11-25T01:00:0${i}Z` }));

        store.addTrades(trades);

        expect(store.getTape().map(t => t.tradeId)).toEqual(['t5', 't4', 't3']);
    });

    it('orders trades by execution time even if they arrive out of order', () => {
        const store = createTradeStore();
        store.addTrades([trade({ tradeId: 'late', timestamp: '2026-11-25T01:00:09Z' }), trade({ tradeId: 'early', timestamp: '2026-11-25T01:00:01Z' })]);

        expect(store.getTape().map(t => t.tradeId)).toEqual(['late', 'early']);
    });

    it('ignores duplicates, such as snapshot and live push overlapping after a reconnect', () => {
        const store = createTradeStore();
        const t = trade();

        expect(store.addTrades([t])).toHaveLength(1);
        expect(store.addTrades([t, { ...t }])).toHaveLength(0);
        expect(store.getTape()).toHaveLength(1);
    });

    it('drops malformed trades rather than rendering them', () => {
        const store = createTradeStore();
        const added = store.addTrades([
            trade({ price: -1 }),
            trade({ quantity: 0 }),
            trade({ side: 'Hold' }),
            trade({ symbol: '' }),
            trade({ timestamp: 'not a date' }),
            null,
            trade(),
        ]);

        expect(added).toHaveLength(1);
    });

    it('tracks per-symbol session statistics', () => {
        const store = createTradeStore();
        store.addTrades([
            trade({ price: 100, quantity: 2, timestamp: '2026-11-25T01:00:01Z' }),
            trade({ price: 110, quantity: 1, timestamp: '2026-11-25T01:00:02Z' }),
            trade({ price: 90, quantity: 1, timestamp: '2026-11-25T01:00:03Z' }),
            trade({ symbol: 'ETHUSDT', price: 3500 }),
        ]);

        const btc = store.getSymbol('BTCUSDT');
        expect(btc).toMatchObject({ open: 100, last: 90, previous: 110, high: 110, low: 90, volume: 4, notional: 400, count: 3 });
        expect(sessionChange(btc)).toBeCloseTo(-10);
        expect(tickDirection(btc)).toBe('down');
        expect(store.getSymbols()).toEqual(['BTCUSDT', 'ETHUSDT']);
    });

    it('filters the tape by symbol', () => {
        const store = createTradeStore();
        store.addTrades([trade(), trade({ symbol: 'ETHUSDT' }), trade()]);

        expect(store.getTape('ETHUSDT')).toHaveLength(1);
        expect(store.getTape()).toHaveLength(3);
    });

    it('accepts numeric strings for price and quantity', () => {
        const store = createTradeStore();
        const [added] = store.addTrades([trade({ price: '95123.45', quantity: '0.5' })]);

        expect(added.price).toBe(95123.45);
        expect(added.value).toBeCloseTo(47561.725);
    });

    it('reports trades per second over a rolling window', () => {
        const store = createTradeStore({ rateWindowMs: 5000 });
        store.addTrades([trade(), trade(), trade(), trade(), trade()], 10_000);

        expect(store.tradesPerSecond(10_000)).toBe(1);
        expect(store.tradesPerSecond(16_000)).toBe(0);
    });

    it('does not count snapshot history towards trades per second', () => {
        const store = createTradeStore({ rateWindowMs: 5000 });
        store.addTrades([trade(), trade(), trade()], 10_000, { live: false });
        store.addTrades([trade()], 10_000);

        expect(store.tradesPerSecond(10_000)).toBe(0.2);
        expect(store.getTape()).toHaveLength(4);
    });

    it('caps sparkline history', () => {
        const store = createTradeStore({ sparkPoints: 3 });
        store.addTrades([1, 2, 3, 4, 5].map(i => trade({ price: i, timestamp: `2026-11-25T01:00:0${i}Z` })));

        expect(store.getSymbol('BTCUSDT').spark).toEqual([3, 4, 5]);
    });
});
