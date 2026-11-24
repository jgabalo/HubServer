---
name: verify
description: Build, test and format-check the HubServer and TradeFeed solutions and the trade-display app. Use before reporting any code change as done, or when asked to build, test, run or verify the app.
---

# Verify HubServer

Run each step from the repo root, in order. On Windows, make sure the working directory has an upper-case drive letter (`C:\`), because Vitest fails with a lower-case one.

1. `dotnet build WebCamHandler/AppHarbor.sln` and `dotnet build TradeFeed/TradeFeed.sln`
   - Warnings are errors, including NuGet vulnerability warnings (NU190x). Fix them by upgrading the package to its latest 10.0.x patch. Never suppress them.
2. `dotnet test WebCamHandler/AppHarbor.sln --no-build` and `dotnet test TradeFeed/TradeFeed.sln --no-build`
   - A failing contract test (`HandlerHubTests`, `ChatterHubTests`, `TradeHubTests`) usually means a hub URL, method or callback name changed. Either restore the contract or, if the user asked for the change, update the tests, `TradeHubContract` / `trade-display/src/hub.js` and the contract tables in `AGENTS.md`, and say so.
3. `dotnet format WebCamHandler/AppHarbor.sln --verify-no-changes` and the same for `TradeFeed/TradeFeed.sln`
   - If it fails, run it without `--verify-no-changes` and review the diff.
4. If `trade-display/` or the trade contract changed:
   - `npm ci --prefix trade-display` (only if `node_modules` is missing or `package-lock.json` changed)
   - `npm test --prefix trade-display`
   - `npm run build --prefix trade-display`
   - A failure in `readonly.test.js` means the display gained a write path or HTML rendering of feed data. Remove it; do not relax the test.
5. Report to the user: build status, test pass/fail counts for each suite, and the format and display build results.
