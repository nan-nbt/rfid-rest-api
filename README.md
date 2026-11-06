# RFIDAPI

RFIDAPI is a .NET 9 ASP.NET Core Web API for UHF RFID readers on Windows x86. It provides secured reader operations and manufacturing and warehouse workflows with a standardized Unified Envelope JSON response pattern across all endpoints.

---

## Table of Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Requirements & Prerequisites](#requirements--prerequisites)
- [Database Schema](#database-schema)
- [Configuration](#configuration)
- [Authentication & RBAC](#authentication--rbac)
- [Build & Run](#build--run)
- [Interactive Web Console](#interactive-web-console)
- [Unified Envelope Response Pattern](#unified-envelope-response-pattern)
- [API Endpoints](#api-endpoints)
  - [Connect](#1-connect-to-rfid-device)
  - [Disconnect](#2-disconnect)
  - [Read Single EPC](#3-read-single-epc)
  - [Write Single EPC](#4-write-single-epc)
  - [Start Inventory Reading](#5-start-inventory-reading)
  - [Stop Inventory Reading](#6-stop-inventory-reading)
  - [Production Scan](#7-production-scan)
  - [Warehouse Stock In](#8-warehouse-stock-in)
  - [Warehouse Stock Out](#9-warehouse-stock-out)
- [Database Management Methods](#database-management-methods)
- [Connection Management & Concurrency](#connection-management--concurrency)
- [HTTP Testing](#http-testing)

---

## Features

- **Device Management**:
  - Connect to UHF RFID reader devices over TCP/IP by IP address and port.
  - Disconnect active device connections cleanly.
  - In-memory connection tracking that checks for an existing connection by client IP and reader address (`ip:port`).
  - Active session tracking using unique GUID connection IDs.
- **RFID Tag Operations**:
  - Read a single EPC tag from the reader buffer. EPC codes are always returned space-formatted (e.g. `00 92 00 00 00 00 00 00 00 00 AC B7`).
  - Write a 24-character hex EPC code to a tag.
  - Start and stop continuous inventory reading.
- **Manufacturing & Warehouse Workflows**:
  - **Production Scan**: Requires the initial blank tag EPC (`000000000000000000000000`), finds a database record by barcode, writes `rfid_code + dem_mk`, and sets `prod_mk` to `Y`.
  - **Warehouse Stock-In**: Finds a database record by the tag's 20-character RFID code, writes the `1111` suffix, and sets `dem_mk` to `1111` and `inout_mk` to `A`.
  - **Warehouse Stock-Out**: Requires a nonempty warehouse mark other than `B`, writes the `0000` suffix, and sets `dem_mk` to `0000` and `inout_mk` to `B`.
- **Unified Envelope Response Pattern**:
  - All endpoints return a consistent JSON envelope with `success`, `status_code`, `message`, `timestamp`, and either a `data` object (success) or an `error` object (failure).
  - All JSON property names use `snake_case`.
  - `status_code` and `message` always reflect real HTTP status codes and their standard uppercase text (e.g. `200` / `"OK"`, `400` / `"BAD REQUEST"`, `404` / `"NOT FOUND"`, `502` / `"BAD GATEWAY"`).
  - Framework-level model binding errors (missing required query parameters) are also intercepted and returned in the same envelope format.
- **Centralized Constants**:
  - All response messages in the controller are defined as constants in the `RFIDAPI.Constants` namespace (`Constants/Common.cs` and `Constants/ConnectionStatus.cs`) for consistency and maintainability.
- **Security & Authorization**:
  - JWT Bearer Authentication integrated with IAM / Keycloak OAuth 2.0.
  - Role-Based Access Control (RBAC) enforcing the `rfid-client-service` role parsed from token `realm_access.roles`.
- **Database & Persistence**:
  - Entity Framework Core with PostgreSQL provider (`IDbContextFactory<AppDbContext>`).
  - Sample barcode entity mapping (`sample_barcode` table).
  - Authenticated list, create, update, and delete operations for sample barcode records. The API limits the table to two records when creating through the management route.
- **Interactive Web Console**:
  - Static API console served from `wwwroot/index.html`, with separate cards for main RFID operations, custom workflows, responses, prerequisites, and database management.
  - The RFID Code field in Database Management auto-formats input as space-separated hex pairs on key-up (e.g. `0092000000000000AB76` → `00 92 00 00 00 00 00 00 AB 76`).
  - Database tables refresh every five seconds while a bearer token is present. Clearing the token clears the displayed records and returns the database status to `Ready`.
  - The navigation bar links to the Contact Information card. The page footer identifies this release as version `1.1.3`.

---

## Technology Stack

- **Framework**: [.NET 9.0](https://dotnet.microsoft.com/) (Target: `x86`)
- **API Engine**: ASP.NET Core Web API
- **Authentication & Security**:
  - `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.0)
  - OAuth 2.0 / OpenID Connect (IAM / Keycloak)
- **Database / ORM**:
  - `Microsoft.EntityFrameworkCore` (9.0.0)
  - `Microsoft.EntityFrameworkCore.Tools` (10.0.7)
  - `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.0)
  - PostgreSQL is the only provider configured for application data. `Oracle.ManagedDataAccess.Core` is also referenced by the project but is not used by the current application data path.
- **API Documentation**:
  - `Microsoft.AspNetCore.OpenApi` (9.0.9)
- **Native UHF Libraries** (copied from `libs/` at build):
  - `UHFAPI.dll` & `UHFControl.dll`
  - Interop libraries (`Microsoft.Office.Interop.Excel.dll`, `Microsoft.Vbe.Interop.dll`, `office.dll`, `stdole.dll`)
  - `Newtonsoft.Json.dll`
- **Application Icons**: `rfid-icon.ico` is the executable icon; `wwwroot/favicon.ico` is served as the browser tab icon.

---

## Project Structure

```text
rfidapi/
├── Configuarations/             # Entity Framework Core DbContext and PostgreSQL persistence configuration
├── Constants/                   # Centralized response message constants (Common.cs, ConnectionStatus.cs)
├── Controllers/                 # Secured RFID and sample barcode API endpoints
├── Helpers/                     # Data conversion, string utilities (FormatEpcWithSpaces), HTTP status helper, and native UHF API wrapper
├── libs/                        # 32-bit native reader and runtime DLL dependencies
├── Managers/                    # In-memory RFID connection tracking
├── Models/                      # API envelope models (ApiResponse, ApiResponseData), RFID connection, tag telemetry, and sample barcode models
├── Properties/                  # HTTP and HTTPS launch profiles
├── Services/                    # RFID reader operations and database workflow methods
├── wwwroot/                     # Interactive console and browser favicon
│   ├── favicon.ico
│   └── index.html
├── appsettings.json             # Application configuration
├── Program.cs                   # Dependency injection, authentication, middleware, routes, and model binding error factory
├── RFIDAPI.csproj               # .NET target, packages, and native dependency copy rules
├── RFIDAPI.http                 # REST Client requests (store credentials securely)
├── RFIDAPI.sln                  # Visual Studio solution
└── rfid-icon.ico                # Windows executable icon
```

### Key Source Files

| File | Purpose |
|---|---|
| `Models/ApiResponse.cs` | Defines `ApiResponse<T>`, `ApiError`, and factory helpers (`Ok`, `Success`, `Error`). |
| `Models/ApiResponseData.cs` | Strongly-typed response DTOs: `DeviceConnectionData`, `TagOperationData`, `MessageData`. |
| `Helpers/HttpStatusHelper.cs` | Maps HTTP status integers to uppercase standard text (`OK`, `BAD REQUEST`, etc.). |
| `Helpers/StringUtils.cs` | `FormatEpcWithSpaces` for space-separating EPC hex strings; `IsHexNumber` validator. |
| `Constants/Common.cs` | All tag, EPC, barcode, workflow, and database management message constants. |
| `Constants/ConnectionStatus.cs` | Connection lifecycle message constants and format strings. |

---

## Requirements & Prerequisites

1. **.NET 9.0 SDK**.
2. **Windows Operating System**: The native UHF reader libraries (`libs/UHFAPI.dll`, `libs/UHFControl.dll`) are 32-bit Windows DLLs. The project targets `x86` and must run in a compatible Windows environment.
3. **PostgreSQL Database**: Accessible database instance hosting the `sample_barcode` table.
4. **IAM / Keycloak Service**: An OAuth 2.0 / OIDC provider issuing JWT access tokens with the `rfid-client-service` role in `realm_access.roles`.
5. **UHF RFID Reader**: Network-enabled UHF RFID reader reachable over TCP/IP.

---

## Database Schema

The API uses PostgreSQL table `sample_barcode`, mapped by [`SampleBarcode`](Models/SampleBarcode.cs) and exposed through [`AppDbContext.SampleBarcodes`](Configuarations/AppDbContext.cs). The repository does not contain a `.sql` schema script or EF Core migrations. The table and columns below are derived from the entity and the database operations in [`UhfService`](Services/UHFService.cs) and [`UhfController`](Controllers/UHFController.cs).

| Column | PostgreSQL type | Required | Description |
|---|---|---:|---|
| `id` | `integer` | Yes | EF Core primary key (recognized by the `id` key convention). |
| `barcode` | `text` | Yes | Production Scan looks up and updates a record by this value. |
| `rfid_code` | `text` | Yes | Warehouse workflows look up a record by this value. It forms the first 20 characters of the 24-character EPC. |
| `dem_mk` | `text` | Yes | Four-character EPC suffix. Stock-in sets it to `1111`; stock-out sets it to `0000`. |
| `prod_mk` | `text` | Yes | Production status. Production Scan sets it to `Y`; warehouse operations reject records marked `N`. |
| `inout_mk` | `text` | No | Warehouse status. Stock-in sets `A`; stock-out sets `B`. A missing or empty value is treated as not yet stocked in. |

The code looks up a single record by `barcode` or `rfid_code`, so values should identify the intended record uniquely. The model does not configure database uniqueness, indexes, or maximum string lengths; add constraints and indexes to match your data and deployment requirements.

Example PostgreSQL DDL for a new database:

```sql
CREATE TABLE sample_barcode (
    id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    barcode text NOT NULL,
    rfid_code text NOT NULL,
    dem_mk text NOT NULL,
    prod_mk text NOT NULL,
    inout_mk text NULL
);
```

For databases using a PostgreSQL `serial` column, ensure its sequence is synchronized with the largest existing `id` after importing or inserting records with explicit IDs. Otherwise, the next generated ID may conflict with an existing primary key.

---

## Configuration

Configuration is managed in `appsettings.json` or through environment variables. Replace the placeholder IAM issuer, audience, PostgreSQL connection string, and Kestrel URL with values for the deployment environment. Do not commit credentials or access tokens.

### Example `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "IamConfig": {
    "Authority": "https://iam.example.com/realms/your-realm",
    "Audience": "your-api-audience"
  },
  "ConnectionStrings": {
    "PostgresConnection": "Host=your-host;Port=5432;Database=your-db;Username=your-user;Password=your-password"
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5083"
      }
    }
  }
}
```

### Key Configuration Sections

| Section | Parameter | Description |
|---|---|---|
| `IamConfig` | `Authority` | OIDC issuer URL of the IAM realm; must match the token's `iss` claim. |
| `IamConfig` | `Audience` | Expected audience (`aud`) in the JWT payload. |
| `ConnectionStrings` | `PostgresConnection` | Connection string for the PostgreSQL database. |
| `Kestrel` | `Endpoints:Http:Url` | HTTP binding address and port. |

### Launch Profiles

Configured in [`Properties/launchSettings.json`](Properties/launchSettings.json):

- **`http`**: `http://localhost:5083` (`ASPNETCORE_ENVIRONMENT=Debug`)
- **`https`**: `https://localhost:7121` and `http://localhost:5083` (`ASPNETCORE_ENVIRONMENT=Debug`)

> [!NOTE]
> The OpenAPI JSON document is mapped in [`Program.cs`](Program.cs) only when `ASPNETCORE_ENVIRONMENT` is `Development`. The supplied `http` and `https` launch profiles set the environment to `Debug`, and the project does not configure a Swagger UI.

---

## Authentication & RBAC

All endpoints under [`UhfController`](Controllers/UHFController.cs) require a valid bearer token with the `rfid-client-service` role. The controller reads role names from the token's `realm_access.roles` claim.

### 1. Acquiring an IAM Access Token

Use the OAuth 2.0 **Client Credentials Grant** against your IAM/Keycloak endpoint:

```http
POST {IamConfig:Authority}/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id={CLIENT_ID}&client_secret={CLIENT_SECRET}
```

### 2. Token Validation & Role Mapping

- Tokens are validated with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, and `ValidateIssuerSigningKey`.
- A 5-minute clock skew tolerance is configured.
- The application automatically parses the Keycloak `realm_access.roles` JSON claim and assigns each role to the caller's `ClaimsIdentity` as `ClaimTypes.Role`.

### 3. Request Header

Pass the obtained access token in the `Authorization` header on all API calls:

```http
Authorization: Bearer <your_access_token>
```

---

## Build & Run

### 1. Restore & Build

```bash
dotnet restore
dotnet build
```

### 2. Run Application

Run with the default or specific launch profile:

```bash
# Default profile
dotnet run

# Using http profile
dotnet run --launch-profile http

# Run with Development environment (enables OpenAPI)
dotnet run --environment Development
```

---

## Interactive Web Console

The browser console is served from the application root (`/`). It separates the standard reader operations from the production and warehouse workflows, displays HTTP responses, and provides a live view of the `sample_barcode` table. Enter a bearer token in Request Setup to load the database tables; they refresh every five seconds while the token is present. Clearing the token clears the displayed rows and resets the database status to `Ready`.

The Database Management card supports listing, creating, editing, and deleting records:

- The **RFID Code** input field automatically formats its display value as space-separated hex pairs on key-up (e.g. typing `0092000000000000AB76` renders as `00 92 00 00 00 00 00 00 AB 76`). Spaces are stripped before the value is sent to the API.
- The form validates: a required barcode, exactly 20 hexadecimal characters for RFID code, exactly four binary digits for DEM mark, and the supported production and warehouse mark values.
- Creating records is limited to two rows.
- When editing an existing record, the RFID Code field is pre-populated with its space-formatted value.

The Contact link in the header navigates to the contacts; the footer shows version `1.1.3`.

---

## Unified Envelope Response Pattern

Every endpoint returns a JSON object with the following top-level shape:

```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T01:47:34.2202207+00:00",
  "data": { }
}
```

On error, the `data` property is omitted and replaced by `error`:

```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:03:31.1824965+00:00",
  "error": {
    "message": "The connection_id 523ac0ec-914b-4677-b85e-19e6d8a1e697 not found."
  }
}
```

### Envelope Fields

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | `true` for 2xx responses, `false` for all errors. |
| `status_code` | `integer` | Real HTTP status code (e.g. `200`, `400`, `404`, `502`). |
| `message` | `string` | Uppercase standard HTTP reason phrase (e.g. `"OK"`, `"BAD REQUEST"`, `"NOT FOUND"`, `"BAD GATEWAY"`). |
| `timestamp` | `string` | ISO 8601 UTC timestamp of the response. |
| `data` | `object` | Present only on success. Shape varies by endpoint. |
| `error` | `object` | Present only on failure. Always contains a `message` string. |

> [!NOTE]
> `null` values inside `data` (e.g. `connection_id: null` on disconnect, `inout_mk: null` for unset marks) are intentionally preserved in the output. Only the top-level `data` and `error` envelope properties are conditionally omitted.

### Missing Required Parameters

If a required query parameter is omitted, the framework model binding error is intercepted and returned in the same envelope format:

```json
{
  "success": false,
  "status_code": 400,
  "message": "BAD REQUEST",
  "timestamp": "2026-11-06T01:52:32.3974133+00:00",
  "error": {
    "message": "The connection_id field is required."
  }
}
```

---

## API Endpoints

Base Route:

```text
/api/uhf
```

All requests require the header:
```http
Authorization: Bearer <access_token>
```

> [!IMPORTANT]
> All EPC codes in responses are formatted with spaces between each byte pair (e.g. `"00 92 00 00 00 00 00 00 00 00 AC B7"`).

---

### 1. Connect to RFID Device

Establishes a TCP connection to an RFID reader.

- **URL**: `GET /api/uhf/connect`
- **Query Parameters**:
  - `ip` (`string`, required): IP address of the RFID device (e.g. `172.19.68.9`).
  - `port` (`uint`, required): Port of the RFID device (e.g. `8888`).

#### Rules & Constraints
- Validates that the client IP is identifiable.
- **Client Check**: Refuses a new connection when the remote client IP already has a tracked session.
- **Device Check**: Refuses a new connection when the requested RFID reader (`ip:port`) is already tracked.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T01:47:34.2202207+00:00",
  "data": {
    "device_address": "172.19.68.9:8888",
    "connection_id": "523ac0ec-914b-4677-b85e-19e6d8a1e697",
    "message": "Connected"
  }
}
```

#### Response (Device unreachable — 502 Bad Gateway)
```json
{
  "success": false,
  "status_code": 502,
  "message": "BAD GATEWAY",
  "timestamp": "2026-11-06T01:48:04.7983770+00:00",
  "error": {
    "message": "Can not connect to RFID device host 172.19.68.9:8888"
  }
}
```

#### Response (Client already connected / Device in use — 400 Bad Request)
```json
{
  "success": false,
  "status_code": 400,
  "message": "BAD REQUEST",
  "timestamp": "2026-11-06T01:48:04.7983770+00:00",
  "error": {
    "message": "This client is already connected"
  }
}
```

---

### 2. Disconnect

Disconnects the active RFID reader connection and removes it from the connection manager.

- **URL**: `GET /api/uhf/disconnect`
- **Query Parameters**:
  - `connectionId` (`string`, required): Connection ID returned by the `connect` endpoint.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T01:47:34.2202207+00:00",
  "data": {
    "device_address": "172.19.68.9:8888",
    "connection_id": null,
    "message": "Disconnected"
  }
}
```

#### Response (Missing parameter — 400 Bad Request)
```json
{
  "success": false,
  "status_code": 400,
  "message": "BAD REQUEST",
  "timestamp": "2026-11-06T01:52:32.3974133+00:00",
  "error": {
    "message": "The connection_id field is required."
  }
}
```

#### Response (Connection not found — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T01:52:32.3974133+00:00",
  "error": {
    "message": "The connection_id 523ac0ec-914b-4677-b85e-19e6d8a1e697 not found."
  }
}
```

---

### 3. Read Single EPC

Reads a single EPC code from the currently positioned tag.

- **URL**: `GET /api/uhf/read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:02:54.2332455+00:00",
  "data": {
    "epc_code": "00 00 00 00 00 00 00 00 00 00 00 00",
    "message": "Tag read successful"
  }
}
```

#### Response (No tag detected — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:03:31.1824965+00:00",
  "error": {
    "message": "The connection_id 523ac0ec-914b-4677-b85e-19e6d8a1e697 not found."
  }
}
```

---

### 4. Write Single EPC

Writes the provided 24-character hex EPC string to the RFID tag.

- **URL**: `POST /api/uhf/write`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.
  - `epcCode` (`string`, required): Exactly 24 hexadecimal characters (spaces are stripped before validation).

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T01:55:03.1287280+00:00",
  "data": {
    "epc_code": "00 92 00 00 00 00 00 00 00 00 AC B7",
    "message": "Tag write successful"
  }
}
```

#### Response (Invalid EPC — 400 Bad Request)
```json
{
  "success": false,
  "status_code": 400,
  "message": "BAD REQUEST",
  "timestamp": "2026-11-06T01:59:19.5999976+00:00",
  "error": {
    "message": "The epc_code field length must be 24-digit hexadecimal code."
  }
}
```

#### Response (Write hardware failure — 502 Bad Gateway)
```json
{
  "success": false,
  "status_code": 502,
  "message": "BAD GATEWAY",
  "timestamp": "2026-11-06T01:59:19.5999976+00:00",
  "error": {
    "message": "Failed to write EPC code to tag. Please ensure the tag is in antenna range"
  }
}
```

---

### 5. Start Inventory Reading

Begins continuous inventory scanning on the reader.

- **URL**: `GET /api/uhf/start-read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

> [!NOTE]
> Re-entrancy is prevented in `UhfService`. If inventory is already running, the service returns `false` and the endpoint responds with `502 Bad Gateway`.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:08:39.2758801+00:00",
  "data": {
    "message": "Start read inventory"
  }
}
```

#### Response (Already scanning — 502 Bad Gateway)
```json
{
  "success": false,
  "status_code": 502,
  "message": "BAD GATEWAY",
  "timestamp": "2026-11-06T02:11:20.2001607+00:00",
  "error": {
    "message": "The device status on start read inventory. Please stop it first."
  }
}
```

---

### 6. Stop Inventory Reading

Stops active inventory scanning on the reader.

- **URL**: `GET /api/uhf/stop-read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:08:39.2758801+00:00",
  "data": {
    "message": "Stop read inventory"
  }
}
```

#### Response (Not scanning — 502 Bad Gateway)
```json
{
  "success": false,
  "status_code": 502,
  "message": "BAD GATEWAY",
  "timestamp": "2026-11-06T02:14:48.9396408+00:00",
  "error": {
    "message": "Failed to stop read inventory."
  }
}
```

---

### 7. Production Scan

Processes an initial manufacturing tag scan and associates it with a database barcode record.

- **URL**: `POST /api/uhf/production-scan`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.
  - `barcode` (`string`, required): Production barcode to match in the database (e.g. `CFM258999L`).

#### Workflow Logic
1. Reads the current tag EPC.
2. Asserts the current EPC is blank (`000000000000000000000000`).
3. Queries `sample_barcode` table for the matching `barcode`.
4. Validates that `prod_mk` is not already `Y`.
5. Writes the new EPC composed of `rfid_code + dem_mk` (24 characters).
6. Updates `sample_barcode.prod_mk` to `Y`.
7. If the database update fails, the controller attempts to rollback the EPC write. On rollback failure, `502 Bad Gateway` is returned.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:19:20.5455693+00:00",
  "data": {
    "id": 12,
    "barcode": "CFM258999R",
    "rfid_code": "0092000000000000AB76",
    "dem_mk": "0000",
    "prod_mk": "Y",
    "inout_mk": null
  }
}
```

#### Response (EPC not blank — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:20:39.4794414+00:00",
  "error": {
    "message": "EPC code not valid. Required EPC code 00 00 00 00 00 00 00 00 00 00 00 00"
  }
}
```

#### Response (Barcode not found — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:20:39.4794414+00:00",
  "error": {
    "message": "[CFM258999R] Barcode does not exist in database"
  }
}
```

#### Response (Already produced — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:20:39.4794414+00:00",
  "error": {
    "message": "[CFM258999R] Barcode is already production scanned"
  }
}
```

---

### 8. Warehouse Stock In

Handles warehouse inventory receipt of a production-scanned item.

- **URL**: `POST /api/uhf/warehouse-stock-in`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Workflow Logic
1. Reads current EPC and extracts the first 20 characters as `rfid_code`.
2. Finds the barcode record in `sample_barcode` by `rfid_code`.
3. Rejects a record marked not produced (`prod_mk == "N"`).
4. Validates that the tag is not already stocked in (`inout_mk != "A"`).
5. Writes new EPC = `rfid_code + "1111"`.
6. Updates database record with `dem_mk = "1111"` and `inout_mk = "A"`.
7. If the database update fails, the controller attempts to restore the EPC using the record's previous `dem_mk` value.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:21:58.6117501+00:00",
  "data": {
    "id": 12,
    "barcode": "CFM258999R",
    "rfid_code": "0092000000000000AB76",
    "dem_mk": "1111",
    "prod_mk": "Y",
    "inout_mk": "A"
  }
}
```

#### Response (Already stocked in — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:23:17.5985344+00:00",
  "error": {
    "message": "00 92 00 00 00 00 00 00 AB 76 11 11 Tag already stocked in warehouse"
  }
}
```

---

### 9. Warehouse Stock Out

Handles warehouse inventory dispatch of a previously stocked-in item.

- **URL**: `POST /api/uhf/warehouse-stock-out`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Workflow Logic
1. Reads current EPC and extracts the first 20 characters as `rfid_code`.
2. Finds the barcode record in `sample_barcode` by `rfid_code`.
3. Rejects a record marked not produced (`prod_mk == "N"`).
4. Requires a nonempty `inout_mk` value (must have been stocked in first).
5. Rejects a record already marked stocked out (`inout_mk == "B"`).
6. Writes new EPC = `rfid_code + "0000"`.
7. Updates database record with `dem_mk = "0000"` and `inout_mk = "B"`.
8. If the database update fails, the controller attempts to restore the EPC using the record's previous `dem_mk` value.

#### Response (Success — 200 OK)
```json
{
  "success": true,
  "status_code": 200,
  "message": "OK",
  "timestamp": "2026-11-06T02:26:38.6553439+00:00",
  "data": {
    "id": 12,
    "barcode": "CFM258999R",
    "rfid_code": "0092000000000000AB76",
    "dem_mk": "0000",
    "prod_mk": "Y",
    "inout_mk": "B"
  }
}
```

#### Response (Already stocked out — 404 Not Found)
```json
{
  "success": false,
  "status_code": 404,
  "message": "NOT FOUND",
  "timestamp": "2026-11-06T02:26:57.2908279+00:00",
  "error": {
    "message": "00 92 00 00 00 00 00 00 AB 76 00 00 Tag already stocked out warehouse"
  }
}
```

---

## Database Management Methods

The interactive console includes a **Database Management** card for listing, creating, editing, and deleting `sample_barcode` records. The displayed tables refresh every five seconds while a bearer token is present. Clearing the token clears the displayed records and resets the database status to `Ready`. On create, the API checks whether two records already exist and returns `409 Conflict` when the limit has been reached; updates and deletes are still available.

The console validates its form inputs in the browser before sending requests:

- `barcode` is required; leading and trailing spaces are removed.
- `rfid_code` must contain exactly 20 hexadecimal characters. The field auto-formats with space pairs as you type; spaces are stripped before submission.
- `dem_mk` must contain exactly four binary digits (`0` or `1`).
- `prod_mk` must be `Y` (produced) or `N` (not produced).
- `inout_mk` may be empty, `A` (in stock), or `B` (out of stock).

The endpoints are secured by the same bearer token and role as the RFID endpoints:

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/uhf/sample-barcode` | List all records, ordered by `id`. |
| `POST` | `/api/uhf/sample-barcode` | Create a record. Returns `409 Conflict` when two records already exist. |
| `PUT` | `/api/uhf/sample-barcode/{id}` | Update a record by ID. |
| `DELETE` | `/api/uhf/sample-barcode/{id}` | Delete a record by ID. Returns `204 No Content`. |

All create/update/delete responses follow the Unified Envelope Pattern. The `DELETE` endpoint returns `204 No Content` (no body) on success.

| Method | Success response | Error responses |
|---|---|---|
| `GET` | `200 OK`, `data` array of records | — |
| `POST` | `201 Created`, `data` with created record | `400 Bad Request` for missing required fields; `409 Conflict` when two records already exist |
| `PUT` | `200 OK`, `data` with updated record | `400 Bad Request` for missing required fields; `404 Not Found` when route ID does not exist |
| `DELETE` | `204 No Content` | `404 Not Found` when route ID does not exist |

---

## Connection Management & Concurrency

The API tracks RFID reader connections in process memory through [`RfidConnectionManager`](Managers/RFIDConnectionManager.cs) using a `ConcurrentDictionary<string, RfidConnection>` keyed by connection ID:

1. **Client Check**: `Connect` checks the remote client IP and declines a new connection when that IP already has a tracked session.
2. **Device Check**: `Connect` checks the reader `ip:port` and declines a new connection when that reader is already tracked.
3. **Session Lifecycle**: Each successful connection receives a unique GUID `connection_id`. All subsequent tag and workflow requests must supply this `connection_id` via the `connectionId` query parameter.
4. **Inventory Guard**: `UhfService` tracks inventory state via `IsInventory`. Calling `start-read` while already scanning returns `502 Bad Gateway`; `stop-read` while not scanning also returns `502 Bad Gateway`.
5. **Cleanup**: Calling `/api/uhf/disconnect` releases the hardware TCP socket and removes the connection session from memory. Sessions are not persisted and are lost when the process stops.

The client and device checks happen before the new session is added and are not a distributed or atomic lock. Simultaneous connection attempts or multiple application instances can therefore bypass the checks.

---

## HTTP Testing

A REST Client request file is available in the repository root:

- [`RFIDAPI.http`](RFIDAPI.http): Contains requests for obtaining an IAM token and calling the API. Treat its IAM client secret and access token values as credentials: replace them with secure local values before use or sharing, and never commit real credentials or tokens.

Run the requests in Visual Studio or Visual Studio Code with the **REST Client** extension.
