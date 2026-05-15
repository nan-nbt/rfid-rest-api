# RFIDAPI

RFIDAPI is an ASP.NET Core Web API for controlling a UHF RFID reader, reading and writing EPC data, and updating RFID-related records in the database.

## Features

- Connect to an RFID device by IP and port
- Disconnect an active connection
- Read a single EPC from the device
- Write a single EPC to the device
- Start and stop inventory reading
- Support business workflows for:
  - Production scan
  - Warehouse stock in
  - Warehouse stock out
- Database-backed RFID lookups and updates through Entity Framework Core
- In-memory connection tracking with per-client and per-device exclusivity

## Technology Stack

- .NET 9.0
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL provider via `Npgsql.EntityFrameworkCore.PostgreSQL`
- Oracle managed driver package
- Native RFID/UHF libraries from the `libs/` folder

## Project Structure

- [`Program.cs`](Program.cs:1) - application startup and service registration
- [`Controllers/UHFController.cs`](Controllers/UHFController.cs:1) - RFID API endpoints
- [`Services/UHFService.cs`](Services/UHFService.cs:1) - RFID device and database operations
- [`Managers/RFIDConnectionManager.cs`](Managers/RFIDConnectionManager.cs:1) - in-memory connection tracking
- [`Configuarations/AppDbContext.cs`](Configuarations/AppDbContext.cs:1) - database context
- [`Models/`](Models) - domain models
- [`Helpers/`](Helpers) - conversion and utility helpers
- [`libs/`](libs) - native or external DLL dependencies required at runtime

## Requirements

- .NET 9 SDK
- x86-compatible runtime environment
- PostgreSQL database access
- RFID UHF reader device reachable over TCP/IP
- Required native DLLs available in `libs/`

## Configuration

The application uses the connection string named `PostgresConnection` from configuration files such as `appsettings.json`.

Example:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Port=5432;Database=rfidapi;Username=postgres;Password=your-password"
  }
}
```

If you use a different database server or credentials, update the connection string accordingly.

### Launch settings

The default launch profiles are configured in [`Properties/launchSettings.json`](Properties/launchSettings.json:1):

- HTTP: `http://localhost:5083`
- HTTPS: `https://localhost:7121;http://localhost:5083`

The launch profiles currently set `ASPNETCORE_ENVIRONMENT` to `dev`, so OpenAPI is not enabled unless you override the environment to `Development`.

## Run the Project

Restore and run the API:

```bash
dotnet restore
dotnet run
```

To use a specific launch profile:

```bash
dotnet run --launch-profile http
dotnet run --launch-profile https
```

To enable OpenAPI, run with the Development environment explicitly:

```bash
dotnet run --environment Development
```

## API Endpoints

Base route:

```text
/api/uhf
```

### Connect to RFID device

```http
GET /api/uhf/connect?ip=127.0.0.1&port=6000
```

Query parameters:

- `ip` - RFID device IP address
- `port` - RFID device port

Response includes a generated `connectionId` used by later requests.

### Disconnect

```http
GET /api/uhf/disconnect?connectionId={connectionId}
```

### Read single EPC

```http
GET /api/uhf/read?connectionId={connectionId}
```

### Write single EPC

```http
POST /api/uhf/write?connectionId={connectionId}&epcCode={24-character-epc}
```

Rules:

- `epcCode` must not be empty
- `epcCode` length must be exactly 24 characters

### Start inventory reading

```http
GET /api/uhf/start-read?connectionId={connectionId}
```

### Stop inventory reading

```http
GET /api/uhf/stop-read?connectionId={connectionId}
```

### Production scan

```http
POST /api/uhf/production-scan?connectionId={connectionId}&barcode={barcode}
```

This endpoint:

- Reads the current EPC from the device
- Finds the barcode record in the database
- Writes the production-scanned EPC
- Updates the barcode production mark

### Warehouse stock in

```http
POST /api/uhf/warehouse-stock-in?connectionId={connectionId}
```

This endpoint:

- Reads the RFID tag
- Looks up the matching RFID code in the database
- Writes the warehouse stock-in EPC
- Updates the in/out mark

### Warehouse stock out

```http
POST /api/uhf/warehouse-stock-out?connectionId={connectionId}
```

This endpoint:

- Reads the RFID tag
- Looks up the matching RFID code in the database
- Writes the warehouse stock-out EPC
- Updates the in/out mark

## Connection Behavior

The API keeps RFID connections in memory through [`RFIDConnectionManager`](Managers/RFIDConnectionManager.cs:3).

Important rules:

- A client can only maintain one active connection at a time
- A device can only be used by one connection at a time
- The returned `connectionId` must be passed to follow-up requests
- Connections are removed from memory when disconnected

## Notes

- The project targets `net9.0`
- The project is configured for `x86`
- OpenAPI is enabled only when `ASPNETCORE_ENVIRONMENT` is `Development`
- The `libs/` directory contains required DLLs that are copied to the output directory during build

## Example Request Flow

1. Connect to the RFID reader
2. Save the returned `connectionId`
3. Call read, write, or workflow endpoints using that `connectionId`
4. Disconnect when finished

## HTTP Test Files

The repository includes HTTP request samples:

- [`RFIDAPI.http`](RFIDAPI.http:1)
- [`test.http`](test.http:1)

Use them to test the API directly from VS Code or compatible tooling.