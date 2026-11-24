// In-memory state for the display: the trade tape and per-symbol session statistics.
// Pure logic with no DOM access, so it can be unit tested.

/**
 * @typedef {{ source: string, symbol: string, tradeId: string, price: number, quantity: number,
 *             side: 'Buy' | 'Sell', timestamp: string }} WireTrade   Shape pushed by the hub.
 * @typedef {WireTrade & { key: string, time: Date, value: number }} Trade
 */

export function createTradeStore({ maxTape = 150, sparkPoints = 120, rateWindowMs = 5000, maxSeen = 5000 } = {}) {
    /** @type {Trade[]} newest first */
    let tape = [];
    const symbols = new Map();
    const seen = new Map();
    const arrivals = [];
    let lastSource = null;

    function remember(key) {
        seen.set(key, true);
        if (seen.size > maxSeen) {
            seen.delete(seen.keys().next().value);
        }
    }

    function stats(symbol) {
        let s = symbols.get(symbol);
        if (!s) {
            s = {
                symbol,
                open: null,
                last: null,
                previous: null,
                high: -Infinity,
                low: Infinity,
                volume: 0,
                notional: 0,
                count: 0,
                lastSide: null,
                lastTime: null,
                spark: [],
            };
            symbols.set(symbol, s);
        }
        return s;
    }

    /**
     * Adds trades (any order). Duplicates, such as overlap between the snapshot fetched on
     * reconnect and live pushes, are ignored. Returns the newly added trades, oldest first.
     * @param {WireTrade[]} incoming
     * @param {number} [now]
     * @param {{ live?: boolean }} [options]  live: false for snapshot history, which must not inflate trades/s.
     * @returns {Trade[]}
     */
    function addTrades(incoming, now = Date.now(), { live = true } = {}) {
        const added = [];

        for (const raw of incoming ?? []) {
            if (!isValid(raw)) continue;

            const key = `${raw.source}:${raw.symbol}:${raw.tradeId}`;
            if (seen.has(key)) continue;
            remember(key);

            const price = Number(raw.price);
            const quantity = Number(raw.quantity);
            added.push({ ...raw, key, price, quantity, value: price * quantity, time: new Date(raw.timestamp) });
        }

        added.sort((a, b) => a.time - b.time);

        for (const trade of added) {
            const s = stats(trade.symbol);
            s.open ??= trade.price;
            s.previous = s.last;
            s.last = trade.price;
            s.high = Math.max(s.high, trade.price);
            s.low = Math.min(s.low, trade.price);
            s.volume += trade.quantity;
            s.notional += trade.value;
            s.count += 1;
            s.lastSide = trade.side;
            s.lastTime = trade.time;
            s.spark.push(trade.price);
            if (s.spark.length > sparkPoints) s.spark.shift();
            lastSource = trade.source;
            if (live) arrivals.push(now);
        }

        if (added.length > 0) {
            tape = added.slice().reverse().concat(tape);
            if (tape.length > maxTape) tape.length = maxTape;
        }

        return added;
    }

    function tradesPerSecond(now = Date.now()) {
        while (arrivals.length > 0 && arrivals[0] < now - rateWindowMs) arrivals.shift();
        return arrivals.length / (rateWindowMs / 1000);
    }

    return {
        addTrades,
        tradesPerSecond,
        /** Newest first, optionally filtered to one symbol. */
        getTape: (symbol = null) => (symbol ? tape.filter(t => t.symbol === symbol) : tape),
        getSymbol: symbol => symbols.get(symbol),
        getSymbols: () => [...symbols.keys()].sort(),
        get source() {
            return lastSource;
        },
    };
}

/** Percentage change from the first price seen this session. */
export function sessionChange(stats) {
    if (!stats || !stats.open) return 0;
    return ((stats.last - stats.open) / stats.open) * 100;
}

/** 'up', 'down' or 'flat' compared with the previous trade. */
export function tickDirection(stats) {
    if (!stats || stats.previous === null || stats.last === stats.previous) return 'flat';
    return stats.last > stats.previous ? 'up' : 'down';
}

function isValid(t) {
    return (
        t &&
        typeof t.symbol === 'string' &&
        t.symbol.length > 0 &&
        t.tradeId != null &&
        Number(t.price) > 0 &&
        Number(t.quantity) > 0 &&
        (t.side === 'Buy' || t.side === 'Sell') &&
        !Number.isNaN(Date.parse(t.timestamp))
    );
}
