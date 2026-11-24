// Read-only connection to HubServer's trade hub.
//
// This app only ever *reads*: it calls GetRecentTrades / GetFeedStatus and listens for pushes.
// It never calls PublishTrades, and the hub rejects publishing from any connection without the
// publisher key, which this app never has.

import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

/** Mirrors TradeFeed.Contracts.TradeHubContract on the server (read-side members only). */
export const TRADE_HUB = Object.freeze({
    getRecentTrades: 'GetRecentTrades',
    getFeedStatus: 'GetFeedStatus',
    tradesReceived: 'tradesReceived',
    feedStatusChanged: 'feedStatusChanged',
});

const MAX_RETRY_MS = 30_000;

/**
 * Connects to the trade hub and keeps the connection alive.
 * @param {{
 *   url: string,
 *   onTrades: (trades: object[], meta: { snapshot: boolean }) => void,
 *   onFeedStatus: (status: { activePublishers: number }) => void,
 *   onConnectionState: (state: 'connecting' | 'connected' | 'reconnecting' | 'disconnected') => void,
 *   recentCount?: number,
 * }} options
 */
export function connectTradeFeed({ url, onTrades, onFeedStatus, onConnectionState, recentCount = 150 }) {
    const connection = new HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: ctx => Math.min(1000 * 2 ** ctx.previousRetryCount, MAX_RETRY_MS),
        })
        .configureLogging(LogLevel.Warning)
        .build();

    let stopped = false;
    let startAttempts = 0;

    connection.on(TRADE_HUB.tradesReceived, trades => onTrades(trades, { snapshot: false }));
    connection.on(TRADE_HUB.feedStatusChanged, status => onFeedStatus(status));

    connection.onreconnecting(() => onConnectionState('reconnecting'));
    connection.onreconnected(async () => {
        onConnectionState('connected');
        await syncSnapshot();
    });
    connection.onclose(() => {
        onConnectionState('disconnected');
        if (!stopped) scheduleStart();
    });

    // Fills the screen on first connect and catches up after a reconnect. The store ignores duplicates.
    async function syncSnapshot() {
        try {
            const [recent, status] = await Promise.all([
                connection.invoke(TRADE_HUB.getRecentTrades, recentCount),
                connection.invoke(TRADE_HUB.getFeedStatus),
            ]);
            onTrades(recent, { snapshot: true });
            onFeedStatus(status);
        } catch (error) {
            console.warn('Could not load the recent trade snapshot.', error);
        }
    }

    async function start() {
        if (stopped) return;
        onConnectionState('connecting');
        try {
            await connection.start();
            startAttempts = 0;
            onConnectionState('connected');
            await syncSnapshot();
        } catch {
            onConnectionState('disconnected');
            scheduleStart();
        }
    }

    function scheduleStart() {
        const delay = Math.min(1000 * 2 ** startAttempts++, MAX_RETRY_MS);
        setTimeout(start, delay);
    }

    start();

    return {
        stop() {
            stopped = true;
            return connection.stop();
        },
    };
}
