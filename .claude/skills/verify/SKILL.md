---
name: verify
description: Build, test and format-check the HubServer solution. Use before reporting any code change as done, or when asked to build, test, run or verify the app.
---

# Verify HubServer

Run each step from the repo root, in order.

1. `dotnet build WebCamHandler/AppHarbor.sln`
   - Warnings are errors, including NuGet vulnerability warnings (NU190x). Fix them by upgrading the package to its latest 10.0.x patch. Never suppress them.
2. `dotnet test WebCamHandler/AppHarbor.sln --no-build`
   - A failing contract test in `HandlerHubTests` or `ChatterHubTests` usually means a hub URL, method or callback name changed. Either restore the public contract or, if the user asked for the change, update the test and the contract table in `AGENTS.md`, and say so.
3. `dotnet format WebCamHandler/AppHarbor.sln --verify-no-changes`
   - If it fails, run it without `--verify-no-changes` and review the diff.
4. Report to the user: build status, test pass/fail counts and the format result.
