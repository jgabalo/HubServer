import './styles.css';
import { connectTradeFeed } from './hub.js';
import { createTradeStore } from './store.js';
import { createView } from './view.js';

const MAX_TAPE_ROWS = 150;
const DEFAULT_HUB_URL = 'http://localhost:52527/signalr/tradeHub';

// ?hub=<url> overrides the build-time VITE_HUB_URL, which is handy for pointing one build at another server.
const hubUrl = new URLSearchParams(location.search).get('hub') || import.meta.env.VITE_HUB_URL || DEFAULT_HUB_URL;

const store = createTradeStore({ maxTape: MAX_TAPE_ROWS });
const view = createView();

let filter = null;
let paused = false;
let pendingWhilePaused = 0;
let incoming = [];
let incomingSnapshot = [];
let frameRequested = false;
let knownSymbols = [];

view.setHubUrl(hubUrl);
view.setConnectionState('connecting');
view.setFeedStatus({ activePublishers: 0 });
view.setPaused(false, 0);
view.renderFilters([], null, selectFilter);

function selectFilter(symbol) {
    filter = symbol;
    view.renderFilters(knownSymbols, filter, selectFilter);
    view.renderTape(store.getTape(filter), MAX_TAPE_ROWS);
}

view.onPauseToggle(() => {
    paused = !paused;
    if (!paused) {
        view.renderTape(store.getTape(filter), MAX_TAPE_ROWS);
        pendingWhilePaused = 0;
    }
    view.setPaused(paused, pendingWhilePaused);
});

// Trades can arrive dozens of times a second; collect them and render once per animation frame.
// Snapshot history (loaded on connect and after reconnects) is kept apart so it neither counts
// towards trades/s nor flashes as "new".
function enqueue(trades, { snapshot = false } = {}) {
    if (!Array.isArray(trades) || trades.length === 0) return;
    (snapshot ? incomingSnapshot : incoming).push(...trades);
    if (!frameRequested) {
        frameRequested = true;
        requestAnimationFrame(flush);
    }
}

function flush() {
    frameRequested = false;
    const now = Date.now();
    const history = store.addTrades(incomingSnapshot, now, { live: false });
    const live = store.addTrades(incoming, now);
    incomingSnapshot = [];
    incoming = [];

    const added = history.concat(live);
    if (added.length === 0) return;

    const symbols = store.getSymbols();
    if (symbols.length !== knownSymbols.length) {
        knownSymbols = symbols;
        view.renderFilters(knownSymbols, filter, selectFilter);
    }

    view.updateTickers(store, new Set(added.map(t => t.symbol)));
    view.setSource(store.source);

    if (paused) {
        pendingWhilePaused += filter ? added.filter(t => t.symbol === filter).length : added.length;
        view.setPaused(true, pendingWhilePaused);
    } else if (history.length > 0) {
        // History can interleave with what is already shown, so redraw the tape in order.
        view.renderTape(store.getTape(filter), MAX_TAPE_ROWS);
    } else {
        view.prependTrades(filter ? live.filter(t => t.symbol === filter) : live, MAX_TAPE_ROWS);
    }
}

connectTradeFeed({
    url: hubUrl,
    onTrades: enqueue,
    onFeedStatus: status => view.setFeedStatus(status),
    onConnectionState: state => view.setConnectionState(state),
    recentCount: MAX_TAPE_ROWS,
});

function tick() {
    view.setClock(new Date());
    view.setRate(store.tradesPerSecond());
}
tick();
setInterval(tick, 1000);

let resizeTimer;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => view.redrawSparklines(store), 150);
});
