# HubServer

This application is for the webcam apps to communicate with each other.

It is an ASP.NET Core SignalR server on .NET 10 that exposes two hubs:

- `/signalr/handlerHub`: webcam component registry and access control
- `/signalr/chatterHub`: user and admin chat relay

## Development

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). It runs on Windows, macOS or Linux.

```bash
dotnet build WebCamHandler/AppHarbor.sln
dotnet test WebCamHandler/AppHarbor.sln
dotnet run --project WebCamHandler/WebCamHandler   # http://localhost:52527
```

## Connecting clients

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
