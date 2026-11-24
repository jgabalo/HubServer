# HubServer

This application is for the webcam apps to communicate with each other.

It is an ASP.NET Core SignalR server on .NET 10 that exposes three hubs:

- `/signalr/handlerHub`: webcam component registry and access control
- `/signalr/chatterHub`: user and admin chat relay
- `/signalr/tradeHub`: live trade relay (see below)

## Live trade relay

```
Binance public trade stream -> TradeFeed.Publisher -> HubServer /signalr/tradeHub -> trade-display (browser)
```

- **[TradeFeed.Publisher](TradeFeed/TradeFeed.Publisher)** is a .NET worker. It streams executed trades from Binance's public market-data WebSocket (no account or API key needed), batches them and publishes them to the hub. It authenticates with a shared publisher key. Set `Publisher:Source` to `Simulated` to use generated trades offline.
- **HubServer** checks the publisher key, validates every trade, keeps the last 500 trades for viewers that join mid-stream, and pushes each batch to all viewers.
- **[trade-display](trade-display)** is a plain JavaScript app that shows the trades in real time. It is read-only: it has no way to publish or place trades, and the hub would reject it if it tried.

Run all three locally, each in its own terminal:

```bash
dotnet run --project WebCamHandler/WebCamHandler          # 1. HubServer on http://localhost:52527
dotnet run --project TradeFeed/TradeFeed.Publisher        # 2. Binance -> hub
cd trade-display && npm install && npm run dev            # 3. http://localhost:5173
```

In Development, HubServer and the publisher share the key `local-dev-publisher-key`. In any other environment, set the same secret in `TradeFeed__PublisherKey` (HubServer) and `Publisher__PublisherKey` (publisher). If the key is not set, HubServer disables publishing.

Binance market data is for display. Check Binance's API terms before using it commercially, and note that Binance is not reachable from some regions. Use the simulated source there.

## Development

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and Node.js 20.19 or later for the trade display. Everything runs on Windows, macOS or Linux.

```bash
dotnet build WebCamHandler/AppHarbor.sln   # HubServer
dotnet test WebCamHandler/AppHarbor.sln
dotnet build TradeFeed/TradeFeed.sln       # trade feed publisher
dotnet test TradeFeed/TradeFeed.sln
npm test --prefix trade-display
```

## Connecting webcam clients

Clients must use the ASP.NET Core SignalR client: `@microsoft/signalr` for JavaScript, or `Microsoft.AspNetCore.SignalR.Client` for .NET. The SignalR 2 clients used with the original .NET Framework version (`jquery.signalR`, `Microsoft.AspNet.SignalR.Client`) are not compatible.

```js
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://<server>/signalr/handlerHub")
    .build();
connection.on("broadCastData", data => console.log(JSON.parse(data)));
await connection.start();
await connection.invoke("AddComponent", JSON.stringify({ Name: "cam-1", IsHost: true }));
```

## Working with AI coding agents

- [AGENTS.md](AGENTS.md) covers the architecture, commands, rules and known issues for any coding agent.
- [CLAUDE.md](CLAUDE.md) imports AGENTS.md and adds Claude Code specifics.
- `.claude/` holds the shared Claude Code settings: permissions and a `/verify` skill.
