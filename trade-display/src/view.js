// DOM rendering. Every value from the feed is written with textContent, never innerHTML,
// so a malformed or hostile message cannot inject markup.

import { formatCompact, formatDateTime, formatNotional, formatPercent, formatPrice, formatQuantity, formatTime, splitSymbol, timeZoneLabel } from './format.js';
import { sessionChange, tickDirection } from './store.js';
import { drawSparkline } from './sparkline.js';

const CONNECTION_LABELS = {
    connecting: 'Connecting',
    connected: 'Live',
    reconnecting: 'Reconnecting',
    disconnected: 'Offline',
};

function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
}

function restartAnimation(node, className) {
    node.classList.remove('flash-up', 'flash-down');
    void node.offsetWidth;
    node.classList.add(className);
}

export function createView(doc = document) {
    const $ = id => doc.getElementById(id);
    const refs = {
        connection: $('conn-status'),
        feed: $('feed-status'),
        rate: $('rate'),
        clock: $('clock'),
        zone: $('tape-zone'),
        tickers: $('tickers'),
        filters: $('filters'),
        pause: $('pause'),
        tapeBody: $('tape-body'),
        tapeEmpty: $('tape-empty'),
        source: $('source-name'),
        hubUrl: $('hub-url'),
    };

    const cards = new Map();
    const css = getComputedStyle(doc.documentElement);
    const colour = name => css.getPropertyValue(name).trim();

    function createCard(symbol) {
        const { base, quote } = splitSymbol(symbol);
        const card = el('article', 'ticker');
        card.dataset.symbol = symbol;

        const head = el('header', 'ticker-head');
        const name = el('h3', 'ticker-symbol', base);
        if (quote) name.append(el('span', 'ticker-quote', `/${quote}`));
        const change = el('span', 'ticker-change', '0.00%');
        head.append(name, change);

        const price = el('div', 'ticker-price', '-');
        const spark = el('canvas', 'ticker-spark');
        spark.setAttribute('aria-hidden', 'true');

        const facts = el('dl', 'ticker-facts');
        const fact = label => {
            const wrap = el('div');
            const value = el('dd', '', '-');
            wrap.append(el('dt', '', label), value);
            facts.append(wrap);
            return value;
        };

        const refsForCard = {
            card,
            change,
            price,
            spark,
            high: fact('High'),
            low: fact('Low'),
            volume: fact(`Vol (${base})`),
            count: fact('Trades'),
        };

        card.append(head, price, spark, facts);
        refs.tickers.append(card);
        cards.set(symbol, refsForCard);
        return refsForCard;
    }

    return {
        setHubUrl(url) {
            refs.hubUrl.textContent = url;
        },

        setConnectionState(state) {
            refs.connection.dataset.state = state;
            refs.connection.querySelector('.label').textContent = CONNECTION_LABELS[state] ?? state;
        },

        setFeedStatus(status) {
            const publishers = status?.activePublishers ?? 0;
            refs.feed.dataset.state = publishers > 0 ? 'online' : 'offline';
            refs.feed.querySelector('.label').textContent = publishers > 0 ? 'Feed online' : 'Feed offline';
            refs.feed.title = `${publishers} active publisher${publishers === 1 ? '' : 's'}`;
        },

        setRate(perSecond) {
            refs.rate.textContent = perSecond.toFixed(1);
        },

        setClock(now) {
            refs.clock.textContent = formatDateTime(now);
            refs.clock.dateTime = now.toISOString();
            refs.zone.textContent = timeZoneLabel(now);
        },

        setSource(name) {
            if (name) refs.source.textContent = name;
        },

        updateTickers(store, symbols) {
            for (const symbol of symbols) {
                const stats = store.getSymbol(symbol);
                if (!stats) continue;
                const card = cards.get(symbol) ?? createCard(symbol);

                const change = sessionChange(stats);
                card.change.textContent = formatPercent(change);
                card.change.dataset.trend = change > 0 ? 'up' : change < 0 ? 'down' : 'flat';

                card.price.textContent = formatPrice(stats.last);
                const direction = tickDirection(stats);
                if (direction !== 'flat') restartAnimation(card.price, direction === 'up' ? 'flash-up' : 'flash-down');

                card.high.textContent = formatPrice(stats.high);
                card.low.textContent = formatPrice(stats.low);
                card.volume.textContent = formatCompact(stats.volume);
                card.count.textContent = stats.count.toLocaleString('en-NZ');

                drawSparkline(card.spark, stats.spark, colour(change < 0 ? '--down' : '--up'));
            }
        },

        redrawSparklines(store) {
            for (const [symbol, card] of cards) {
                const stats = store.getSymbol(symbol);
                drawSparkline(card.spark, stats.spark, colour(sessionChange(stats) < 0 ? '--down' : '--up'));
            }
        },

        renderFilters(symbols, active, onSelect) {
            const options = [null, ...symbols];
            refs.filters.replaceChildren(
                ...options.map(symbol => {
                    const button = el('button', 'chip', symbol ? splitSymbol(symbol).base : 'All');
                    button.type = 'button';
                    button.setAttribute('aria-pressed', String(symbol === active));
                    button.addEventListener('click', () => onSelect(symbol));
                    return button;
                }),
            );
        },

        /** Replaces the whole tape (used on filter change or resume). */
        renderTape(trades, maxRows) {
            refs.tapeBody.replaceChildren(...trades.slice(0, maxRows).map(t => tradeRow(t, false)));
            refs.tapeEmpty.hidden = trades.length > 0;
        },

        /** Adds newly arrived trades (oldest first) to the top of the tape. */
        prependTrades(trades, maxRows) {
            if (trades.length === 0) return;
            const fragment = doc.createDocumentFragment();
            for (let i = trades.length - 1; i >= 0; i--) fragment.append(tradeRow(trades[i], true));
            refs.tapeBody.prepend(fragment);
            while (refs.tapeBody.rows.length > maxRows) refs.tapeBody.deleteRow(-1);
            refs.tapeEmpty.hidden = true;
        },

        setPaused(paused, pending) {
            refs.pause.setAttribute('aria-pressed', String(paused));
            refs.pause.textContent = paused ? `Resume${pending > 0 ? ` (${pending.toLocaleString('en-NZ')} new)` : ''}` : 'Pause';
        },

        onPauseToggle(handler) {
            refs.pause.addEventListener('click', handler);
        },
    };
}

function tradeRow(trade, isNew) {
    const row = el('tr', `side-${trade.side.toLowerCase()}${isNew ? ' is-new' : ''}`);
    const { base, quote } = splitSymbol(trade.symbol);

    const time = el('td', 'col-time');
    const timeEl = el('time', '', formatTime(trade.time));
    timeEl.dateTime = trade.time.toISOString();
    time.append(timeEl);

    const symbol = el('td', 'col-symbol', base);
    if (quote) symbol.append(el('span', 'muted', `/${quote}`));

    const side = el('td', 'col-side');
    side.append(el('span', 'side-tag', trade.side === 'Buy' ? '▲ Buy' : '▼ Sell'));

    row.append(
        time,
        symbol,
        side,
        el('td', 'num col-price', formatPrice(trade.price)),
        el('td', 'num col-qty', formatQuantity(trade.quantity)),
        el('td', 'num col-value', formatNotional(trade.value)),
    );
    return row;
}
