# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.

## What this is

HubServer is a SignalR relay that lets the webcam client apps (which live outside this repo) find each other and exchange access and chat messages. It is an **ASP.NET Core MVC + ASP.NET Core SignalR app on .NET 10**. It has no database, and all state is held in memory.

It also hosts a **live trade relay**. `TradeFeed.Publisher` streams public trades from Binance into the hub, and the browser app `trade-display` shows them in real time, read-only:

```
Binance public trade stream -> TradeFeed.Publisher (.NET worker) -> HubServer /signalr/tradeHub -> trade-display (browser, read-only)
```

HubServer was migrated from ASP.NET MVC 5 / SignalR 2 on .NET Framework 4.5. Clients written for SignalR 2 (`jquery.signalR`, `Microsoft.AspNet.SignalR.Client`) **cannot** connect to it. Clients must use `@microsoft/signalr` (JavaScript) or `Microsoft.AspNetCore.SignalR.Client` (.NET).

## Commands

Run from the repo root. The SDK version is pinned in `global.json` (10.0.x). There are two .NET solutions:

- `WebCamHandler/AppHarbor.sln`: HubServer and its tests. It also lists `TradeFeed.Contracts`, which HubServer references.
- `TradeFeed/TradeFeed.sln`: the trade contracts, the publisher and its tests. `TradeFeed.Publisher.Tests` references `HubServer.csproj` to run end-to-end tests against an in-memory hub, so building it also builds HubServer.

| Task | Command |
| --- | --- |
| Build | `dotnet build WebCamHandler/AppHarbor.sln` and `dotnet build TradeFeed/TradeFeed.sln` |
| Test | `dotnet test WebCamHandler/AppHarbor.sln` and `dotnet test TradeFeed/TradeFeed.sln` |
| Format check | `dotnet format <solution> --verify-no-changes` for each solution |
| Run | `dotnet run --project WebCamHandler/WebCamHandler` (serves `http://localhost:52527`) |
| Run publisher | `dotnet run --project TradeFeed/TradeFeed.Publisher` (needs HubServer running) |
| Display: install | `npm ci --prefix trade-display` |
| Display: test | `npm test --prefix trade-display` |
| Display: build | `npm run build --prefix trade-display` |
| Display: dev server | `npm run dev --prefix trade-display` (serves `http://localhost:5173`) |

- All .NET projects set `TreatWarningsAsErrors`. That includes NuGet vulnerability warnings (NU190x), so a vulnerable package version fails the build. Fix it by upgrading the package, not by suppressing the warning.
- The tests in `HubServer.Tests` and `TradeFeed.Publisher.Tests` host the app in memory (`WebApplicationFactory<Program>`) and connect real SignalR clients, so they cover the whole pipeline. They do not need a running server or network access. Only the publisher's live Binance source needs the internet, and no test uses it.
- On Windows, Vitest fails every file with `Cannot read properties of undefined (reading 'config')` when the working directory has a lower-case drive letter (`c:\`). Run npm commands from an upper-case `C:\` path.
- In Development, HubServer and the publisher share the publisher key `local-dev-publisher-key` from their `appsettings.Development.json` files. Elsewhere, set `TradeFeed__PublisherKey` (HubServer) and `Publisher__PublisherKey` (publisher) to the same secret. Never commit a real key.

## Layout

```
global.json                     Pins the .NET 10 SDK
WebCamHandler/
  AppHarbor.sln                 HubServer solution
  WebCamHandler/                Web project (folder name differs from project name)
    HubServer.csproj            SDK-style, net10.0, assembly WebCamHandler.dll
    Program.cs                  DI, CORS, MVC routes, hub endpoints
    Hubs/
      HubRoutes.cs                Hub URL paths (public contract)
      HandlerHub.cs, Handler.cs   Webcam component registry and access control
      ChatterHub.cs, Chatter.cs   User <-> admin chat relay
    Trading/
      TradeHub.cs                 Trade relay hub: publisher auth, validation, broadcast
      TradeStore.cs               Recent-trade buffer and publisher count (singleton)
      TradeFeedOptions.cs         "TradeFeed" config section
    Models/SystemModels.cs      AccessControl, UserData DTOs
    Controllers/HomeController.cs  Static admin pages
    Views/                      Razor views (_ViewImports, _Layout, Home/*)
    wwwroot/                    Bootstrap 3, jQuery 2.2 (vendored, served as-is)
  HubServer.Tests/              xUnit integration tests for HubServer
TradeFeed/
  TradeFeed.sln                 Solution for the trade feed
  TradeFeed.Contracts/          Trade, TradeSide, FeedStatus, TradeHubContract (shared wire contract, also used by HubServer)
  TradeFeed.Publisher/          .NET worker: trade source -> batcher -> hub client
    Sources/                    BinanceTradeSource (+ parser), SimulatedTradeSource
    Publishing/                 TradeBatcher, SignalRTradeHubClient
  TradeFeed.Publisher.Tests/    Parser, batcher and end-to-end tests (reference HubServer)
trade-display/                  Read-only browser app (plain JS + Vite + Vitest)
  src/hub.js, store.js, view.js, format.js, main.js
```

## Architecture

- **Hub + singleton pattern.** Each hub (`HandlerHub`, `ChatterHub`) is a thin, per-call class that delegates to a DI singleton (`Handler`, `Chatter`) registered in `Program.cs`. The singleton holds the state in `List<T>` fields and pushes to clients through `IHubContext<THub>`. Follow this pattern: keep hubs thin and put logic and state in the singleton.
- **Wire format.** Hub methods take **JSON strings** and parse them with Newtonsoft.Json (`AccessControl`, `UserData`, `Dictionary<string,string>`) rather than typed parameters. Newtonsoft is kept on purpose: it parses property names case-insensitively and writes PascalCase, which the clients depend on. Do not switch to System.Text.Json without the user's agreement.
- **Public contract.** The hub URLs, server method names and client callback names are used by external clients. Treat them as a public API, and do not change them without the user's agreement. The contract tests in `HubServer.Tests` fail when the contract changes.

  | Hub URL (`HubRoutes`) | Server methods | Client callbacks |
  | --- | --- | --- |
  | `/signalr/handlerHub` | `Send`, `AllowAccess`, `AddComponent`, `RemoveComponent` | `addNewMessageToPage`, `processAccess`, `broadCastData` |
  | `/signalr/chatterHub` | `UserAccess`, `AdminAccess`, `GetUserData`, `SendMessageToAdmin`, `SendMessageToUser` | `sendAdminMessage`, `sendUserData`, `sendUserMessage` |

- **CORS** allows any origin with credentials, which matches the old `CorsOptions.AllowAll`. JSONP is not supported by ASP.NET Core SignalR and was dropped.

### Trade relay

- **Roles.** A connection that sends a valid `X-Publisher-Key` header is a *publisher*. Every other connection is a *viewer*, which joins the `viewers` group and can only read. `PublishTrades` throws a `HubException` for viewers, and publishing is disabled entirely when `TradeFeed:PublisherKey` is empty.
- **Typed contract.** Unlike the legacy hubs, the trade hub uses typed parameters (`Trade[]`, `FeedStatus`) serialised by SignalR's System.Text.Json protocol: camelCase properties, `side` as `"Buy"`/`"Sell"` and ISO-8601 timestamps. The names live in `TradeFeed.Contracts.TradeHubContract`, and `trade-display/src/hub.js` mirrors the read-side names.

  | Member | Direction | Who |
  | --- | --- | --- |
  | `PublishTrades(Trade[])` (max 200 per call) | client -> hub | Publisher only |
  | `GetRecentTrades(int max)` -> `Trade[]`, oldest first | client -> hub | Anyone |
  | `GetFeedStatus()` -> `FeedStatus` | client -> hub | Anyone |
  | `tradesReceived(Trade[])` | hub -> viewers | |
  | `feedStatusChanged(FeedStatus)` | hub -> viewers | |

- **Publisher pipeline.** An `ITradeSource` (Binance WebSocket or simulated) feeds `TradeBatcher`, which flushes every `BatchIntervalMilliseconds` in chunks of up to `MaxBatchSize`, through `SignalRTradeHubClient`. Both sides reconnect with exponential back-off. Batches are dropped, not queued, while the hub is unreachable, because they are live data.
- **Display.** `store.js` is pure and unit-tested. `view.js` writes feed values with `textContent` only. Rendering is batched per animation frame because busy markets send dozens of trades a second.

## Rules for changes

1. **Test every behaviour change.** Add or update a test in `HubServer.Tests`. Hub tests create a fresh `WebApplicationFactory` for each test class instance so the singleton state does not leak between tests. Keep it that way.
2. **Manage packages with `<PackageReference>`.** Keep ASP.NET Core packages on the latest 10.0.x patch.
3. **Match the existing style:** C# files are UTF-8 with BOM, CRLF line endings, 4-space indentation, Allman braces, block-scoped namespaces, `_camelCase` private fields and `///` XML doc comments on public members (see `.editorconfig`). Nullable reference types are enabled.
4. **Keep changes scoped.** The known issues below are real, but only fix one when the task calls for it, or call it out instead of fixing it silently. Some tests deliberately pin the current, buggy behaviour; update them when you fix the bug.
5. **Before you finish:** run the build, test and format-check commands, plus the display's test and build if you touched `trade-display`, and report the results.
6. **The display must stay read-only.** Never add publishing, order entry or any call that changes server state to `trade-display`. `src/readonly.test.js` enforces this. Never render feed data with `innerHTML`.
7. **Change the trade contract in one place.** Update `TradeHubContract`, the hub, `trade-display/src/hub.js` and the contract tests together. Keep JavaScript dependencies pinned to exact versions.
8. **Never commit** `bin/`, `obj/`, `.vs/`, `node_modules/`, `trade-display/dist/`, `.env.local`, publish profiles (`*.pubxml`), local appsettings overrides or secrets.

## Known issues (pre-existing, do not assume they are intentional)

- `Handler.RemoveComponent` calls `List.Remove` with a newly deserialised object. Reference equality means it never removes anything. The test `RemoveComponent_BroadcastsMasterList_KnownBugDoesNotRemove` pins this behaviour.
- `Chatter.SendAdminMessage` throws `NullReferenceException` if the sender never called `UserAccess`. The client receives a hub error.
- Neither hub overrides `OnDisconnectedAsync`, so disconnected clients stay in the in-memory lists.
- Shared `List<T>` state is mutated from concurrent hub calls without locking.
- `AdminAccess` trusts a client-supplied connection ID instead of `Context.ConnectionId`, and CORS allows any origin. Any client can register as admin and receive user name, email and phone data.
- `Handler.AddComponent` and `Handler.RemoveComponent` swallow all exceptions.
- `Handler`/`HandlerHub` use the `WebCamHandler` namespace although they live in `Hubs/`, unlike `Chatter` (`WebCamHandler.Hubs`).
- The vendored front-end libraries are old (jQuery 2.2.0, Bootstrap 3.3.6) and have published advisories.
