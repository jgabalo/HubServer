# Trade Display

A read-only, real-time trade screen. It connects to HubServer's trade hub and shows trades as they happen. It cannot place, modify or publish trades.

- Plain JavaScript (ES modules) with [Vite](https://vite.dev) for the dev server and build, and `@microsoft/signalr` for the hub connection.
- Ticker cards for each symbol show the last price, change since the page opened, high, low, volume, trade count and a sparkline.
- A live trade tape can be filtered by symbol and paused.
- Times are shown in New Zealand time (Pacific/Auckland) and dates as DD/MM/YYYY.

## Run it

Start HubServer and the publisher first (see the root [README](../README.md)), then:

```bash
npm install
npm run dev        # http://localhost:5173
```

| Command | Purpose |
| --- | --- |
| `npm run dev` | Dev server with hot reload |
| `npm test` | Unit tests (Vitest) |
| `npm run build` | Production build into `dist/`, deployable to any static host |
| `npm run preview` | Serve the production build on http://localhost:4173 |

## Configuration

The hub URL defaults to `http://localhost:52527/signalr/tradeHub`. You can override it in two ways:

- At build time: copy `.env.example` to `.env.local` and set `VITE_HUB_URL`.
- At run time: add `?hub=https://your-server/signalr/tradeHub` to the page URL.

HubServer's CORS policy accepts any origin, so the display can be hosted on a different domain from the hub.

## How it works

| File | Role |
| --- | --- |
| `src/hub.js` | Connects to the hub and reconnects automatically. Loads a snapshot of recent trades on connect and after reconnects, then listens for `tradesReceived` and `feedStatusChanged`. |
| `src/store.js` | Pure state: the trade tape (newest first, capped), de-duplication, per-symbol statistics and trades per second. |
| `src/view.js` | DOM rendering. Feed values are written with `textContent` only. |
| `src/main.js` | Wiring. Collects incoming trades and renders them once per animation frame. |
| `src/format.js` | Auckland time, DD/MM/YYYY and en-NZ number formatting. |

`src/readonly.test.js` fails the build if any app code references `PublishTrades`, sends to the hub, or uses `innerHTML`.

## Troubleshooting

- **Feed offline:** HubServer is reachable but no publisher is connected. Start `TradeFeed.Publisher`.
- **Offline / Reconnecting:** HubServer is not reachable at the configured URL.
- **Every Vitest file fails with `Cannot read properties of undefined (reading 'config')` on Windows:** the shell's working directory uses a lower-case drive letter (`c:\...`), so Vitest loads itself twice. Run the tests from a path with an upper-case drive letter, for example `cd C:\PersonalProjects\HubServer\trade-display`.
