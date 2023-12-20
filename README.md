# WebSocket

## Introduction

This is a simple WebSocket server and client implementation in C# (no external dependencies)

## Usage

### Server

run the server with the following command:

```bash
dotnet run --project .\src\Server\Server.csproj
```

the server will listen on port `5100` by default, you can change it at the [`lauchSettings.json`](./src/Server/Properties/launchSettings.json) file.

### Client

run the client with the following command:

```bash
dotnet run --project .\src\Client\Client.csproj
```

the client will connect to `ws://localhost:5100` by default, you can change it at the [`Program.cs`](./src/Client/Program.cs) file.

