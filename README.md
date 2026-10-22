# RFIDAPI

RFIDAPI is a .NET 9 ASP.NET Core Web API for UHF RFID readers on Windows x86. It provides secured reader operations and manufacturing and warehouse workflows.

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
- [Connection Management & Concurrency](#connection-management--concurrency)
- [HTTP Testing](#http-testing)

---

## Features

- **Device Management**:
  - Connect to UHF RFID reader devices over TCP/IP by IP address and port.
  - Disconnect active device connections cleanly.
  - In-memory exclusive connection management per client IP and per RFID device host (`ip:port`).
  - Active session tracking using unique GUID connection IDs.
- **RFID Tag Operations**:
  - Read a single EPC tag from the reader buffer.
  - Write a 24-character hex EPC code to a tag.
  - Start and stop continuous inventory reading.
- **Manufacturing & Warehouse Workflows**:
  - **Production Scan**: Validates blank/initial tag EPC (`000000000000000000000000`), matches database barcode record, encodes barcode EPC (`rfid_code` + `dem_mk`), marks production status (`prod_mk = 'Y'`), with automatic EPC rollback on DB failure.
  - **Warehouse Stock-In**: Reads tag, validates production scan status, encodes stock-in EPC (`rfid_code` + `1111`), updates inventory mark (`inout_mk = 'A'`), with EPC rollback on DB failure.
  - **Warehouse Stock-Out**: Reads tag, validates prior stock-in status, encodes stock-out EPC (`rfid_code` + `0000`), updates inventory mark (`inout_mk = 'B'`), with EPC rollback on DB failure.
- **Security & Authorization**:
  - JWT Bearer Authentication integrated with IAM / Keycloak OAuth 2.0.
  - Role-Based Access Control (RBAC) enforcing the `rfid-client-service` role parsed from token `realm_access.roles`.
- **Database & Persistence**:
  - Entity Framework Core with PostgreSQL provider (`IDbContextFactory<AppDbContext>`).
  - Sample barcode entity mapping (`sample_barcode` table).

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
  - `Oracle.ManagedDataAccess.Core` (23.26.200)
- **API Documentation**:
  - `Microsoft.AspNetCore.OpenApi` (9.0.9)
- **Native UHF Libraries** (copied from `libs/` at build):
  - `UHFAPI.dll` & `UHFControl.dll`
  - Interop libraries (`Microsoft.Office.Interop.Excel.dll`, `Microsoft.Vbe.Interop.dll`, `office.dll`, `stdole.dll`)
  - `Newtonsoft.Json.dll`
- **Application Icon**: `rfid-icon.ico`

---

## Project Structure

```text
rfidapi_test/
|-- Configuarations/AppDbContext.cs
|-- Constants/                   # API status and response constants
|-- Controllers/UHFController.cs # RFID API endpoints
|-- Helpers/                     # Conversion, validation, native API wrapper
|-- Managers/RFIDConnectionManager.cs
|-- Models/                      # RFID connection and sample barcode models
|-- Services/UHFService.cs       # Reader operations and database workflows
|-- libs/                        # Native and interop DLLs
|-- Properties/launchSettings.json
|-- appsettings*.json
|-- Program.cs
|-- RFIDAPI.csproj
|-- RFIDAPI.http
`-- UHFTAGInfo.cs
```

## Requirements & Prerequisites

1. **.NET 9.0 SDK** (32-bit / x86 compatible)
2. **Windows Operating System**: The native UHF reader libraries (`libs/UHFAPI.dll`, `libs/UHFControl.dll`) are 32-bit Windows DLLs. The project must be built and run on Windows targeting `x86`.
3. **PostgreSQL Database**: Accessible database instance hosting the `sample_barcode` table.
4. **IAM / Keycloak Service**: An OAuth 2.0 / OIDC provider issuing JWT access tokens.
5. **UHF RFID Reader**: Network-enabled UHF RFID reader reachable over TCP/IP.

## Database Schema

The API uses PostgreSQL table `sample_barcode`, mapped by [`SampleBarcode`](Models/SampleBarcode.cs) and exposed through [`AppDbContext.SampleBarcodes`](Configuarations/AppDbContext.cs). The repository does not contain a `.sql` schema script. The native RFID libraries handle reader communication and do not define the database schema, so the table and columns below are derived from the entity and the database operations in [`UhfService`](Services/UHFService.cs) and [`UhfController`](Controllers/UHFController.cs).

| Column | PostgreSQL type | Required | Description |
|---|---|---:|---|
| `id` | `integer` | Yes | EF Core primary key (recognized by the `id` key convention). |
| `barcode` | `text` | Yes | Production Scan looks up and updates a record by this value. |
| `rfid_code` | `text` | Yes | Warehouse workflows look up a record by this value. It forms the first 20 characters of the 24-character EPC. |
| `dem_mk` | `text` | Yes | Four-character EPC suffix. Stock-in sets it to `1111`; stock-out sets it to `0000`. |
| `prod_mk` | `text` | Yes | Production status. Production Scan sets it to `Y`; warehouse operations reject records marked `N`. |
| `inout_mk` | `text` | No | Warehouse status. Stock-in sets `A`; stock-out sets `B`. A missing or empty value is treated as not yet stocked in. |

The code compares `barcode` and `rfid_code` to find a single record, so each should identify the intended record uniquely. The model does not configure database uniqueness, indexes, or maximum string lengths; add constraints and indexes to match your data and deployment requirements.

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

The SQL types and nullability above reflect the current C# model and PostgreSQL provider conventions. The example does not specify uniqueness, indexes, or maximum string lengths because these are not defined in the project model. If your deployment schema specifies them, retain those definitions.

---

## Configuration

Configuration is managed via JSON configuration files (`appsettings.json`, `appsettings.dev.json`, or environment variables).

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
    "Authority": "https://iamlab.pouchen.com/auth/realms/service",
    "Audience": "account"
  },
  "ConnectionStrings": {
    "PostgresConnection": "Host=your-host;Port=5432;Database=your-db;Username=your-user;Password=your-password"
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5083"
      }
    }
  }
}
```

### Key Configuration Sections

| Section | Parameter | Description |
|---|---|---|
| `IamConfig` | `Authority` | OIDC issuer URL of the IAM realm (e.g. `https://iamlab.pouchen.com/auth/realms/service`). |
| `IamConfig` | `Audience` | Expected audience (`aud`) in the JWT payload (e.g. `account`). |
| `ConnectionStrings` | `PostgresConnection` | Connection string for the PostgreSQL database. |
| `Kestrel` | `Endpoints:Http:Url` | Binding address and port (default: `http://0.0.0.0:5083`). |

### Launch Profiles

Configured in [`Properties/launchSettings.json`](Properties/launchSettings.json):

- **`http`**: `http://localhost:5083` (`ASPNETCORE_ENVIRONMENT=dev`)
- **`https`**: `https://localhost:7121;http://localhost:5083` (`ASPNETCORE_ENVIRONMENT=dev`)

> [!NOTE]
> OpenAPI / Swagger is enabled in [`Program.cs`](Program.cs) when `ASPNETCORE_ENVIRONMENT` is `Development`. When running with the `dev` profile, OpenAPI endpoints are not mapped unless `ASPNETCORE_ENVIRONMENT` is explicitly set to `Development`.

---

## Authentication & RBAC

All endpoints under [`UHFController`](Controllers/UHFController.cs) require a valid bearer token and the `rfid-client-service` role.

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

## API Endpoints

Base Route:

```text
/api/uhf
```

All requests require the header:
```http
Authorization: Bearer <access_token>
```

---

### 1. Connect to RFID Device

Establishes a TCP connection to an RFID reader.

- **URL**: `GET /api/uhf/connect`
- **Query Parameters**:
  - `ip` (`string`, required): IP address of the RFID device (e.g. `172.19.68.9`).
  - `port` (`uint`, required): Port of the RFID device (e.g. `8888`).

#### Rules & Constraints
- Validates that the client IP is identifiable.
- **Client Exclusivity**: A client IP can maintain only one active connection at a time.
- **Device Exclusivity**: An RFID reader (`ip:port`) can only be connected to one client at a time.

#### Response (Success - 200 OK)
```json
{
  "result": true,
  "connectionId": "34a9de02-2ff3-4998-a38d-66c65d846c88",
  "status": "Connected"
}
```

#### Response (Client already connected - 200 OK)
```json
{
  "result": false,
  "status": "This client already connected"
}
```

#### Response (Device already in use - 200 OK)
```json
{
  "result": false,
  "status": "RFID device already in use"
}
```

---

### 2. Disconnect

Disconnects the active RFID reader connection and removes it from the connection manager.

- **URL**: `GET /api/uhf/disconnect`
- **Query Parameters**:
  - `connectionId` (`string`, required): Connection ID returned by the `connect` endpoint.

#### Response (Success - 200 OK)
```json
{
  "result": true,
  "status": "Disconnected"
}
```

#### Response (Not Found - 404)
```json
{
  "result": false,
  "status": "Connection not found"
}
```

---

### 3. Read Single EPC

Reads a single EPC code from the currently positioned tag.

- **URL**: `GET /api/uhf/read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Response (Success - 200 OK)
```json
{
  "epcCode": "000000000000000000000000"
}
```

#### Response (Error - 404)
```json
{
  "message": "Tag read EPC code failed"
}
```

---

### 4. Write Single EPC

Writes the provided 24-character EPC string to the RFID tag. The endpoint checks its length; provide hexadecimal content for the reader.

- **URL**: `POST /api/uhf/write`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.
  - `epcCode` (`string`, required): Exactly 24 characters. Supply a hexadecimal EPC value.

#### Response (Success - 200 OK)
```json
{
  "result": true,
  "epcCode": "000000000000000000000000",
  "status": "Success"
}
```

#### Response (Validation Error - 404)
```json
{
  "message": "EPC code not valid. length must be 24 digit aplhanumeric"
}
```

---

### 5. Start Inventory Reading

Begins continuous inventory scanning on the reader.

- **URL**: `GET /api/uhf/start-read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Response (200 OK)
```json
{
  "result": true,
  "status": "Start read inventory"
}
```

---

### 6. Stop Inventory Reading

Stops active inventory scanning on the reader.

- **URL**: `GET /api/uhf/stop-read`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Response (200 OK)
```json
{
  "result": true,
  "status": "Stop read inventory"
}
```

---

### 7. Production Scan

Processes an initial manufacturing tag scan and associates it with a database barcode.

- **URL**: `POST /api/uhf/production-scan`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.
  - `barcode` (`string`, required): Production barcode to match in database (e.g. `CFM258999L`).

#### Workflow Logic:
1. Reads the current tag EPC.
2. Asserts the current EPC is blank (`000000000000000000000000`).
3. Queries `sample_barcode` table for the matching `barcode`.
4. Validates that `prod_mk` is not already `'Y'`.
5. Writes the new EPC composed of `rfid_code + dem_mk` (24 characters).
6. Updates `sample_barcode.prod_mk` to `'Y'`.
7. **Rollback**: If the database update fails, the EPC on the physical tag is rolled back.

#### Response (Success - 200 OK)
```json
{
  "result": {
    "id": 1,
    "barcode": "CFM258999L",
    "rfid_code": "E2801191200000000000",
    "dem_mk": "0000",
    "prod_mk": "Y",
    "inout_mk": null
  },
  "status": "success"
}
```

---

### 8. Warehouse Stock In

Handles warehouse inventory receipt of a production-scanned item.

- **URL**: `POST /api/uhf/warehouse-stock-in`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Workflow Logic:
1. Reads current EPC and extracts the first 20 characters as `rfid_code`.
2. Finds the barcode record in `sample_barcode` by `rfid_code`.
3. Validates that the tag was production scanned (`prod_mk == "Y"`).
4. Validates that the tag is not already stocked in (`inout_mk != "A"`).
5. Writes new EPC = `rfid_code + "1111"`.
6. Updates database record with `dem_mk = "1111"` and `inout_mk = "A"`.
7. **Rollback**: If the database update fails, the EPC on the physical tag is rolled back.

#### Response (Success - 200 OK)
```json
{
  "result": {
    "id": 1,
    "barcode": "CFM258999L",
    "rfid_code": "E2801191200000000000",
    "dem_mk": "1111",
    "prod_mk": "Y",
    "inout_mk": "A"
  },
  "status": "success"
}
```

---

### 9. Warehouse Stock Out

Handles warehouse inventory dispatch of a previously stocked-in item.

- **URL**: `POST /api/uhf/warehouse-stock-out`
- **Query Parameters**:
  - `connectionId` (`string`, required): Active connection ID.

#### Workflow Logic:
1. Reads current EPC and extracts the first 20 characters as `rfid_code`.
2. Finds the barcode record in `sample_barcode` by `rfid_code`.
3. Validates that the tag was production scanned (`prod_mk == "Y"`).
4. Validates that the tag was previously stocked in (`inout_mk` is not null or empty).
5. Validates that the tag is not already stocked out (`inout_mk != "B"`).
6. Writes new EPC = `rfid_code + "0000"`.
7. Updates database record with `dem_mk = "0000"` and `inout_mk = "B"`.
8. **Rollback**: If the database update fails, the EPC on the physical tag is rolled back.

#### Response (Success - 200 OK)
```json
{
  "result": {
    "id": 1,
    "barcode": "CFM258999L",
    "rfid_code": "E2801191200000000000",
    "dem_mk": "0000",
    "prod_mk": "Y",
    "inout_mk": "B"
  },
  "status": "success"
}
```

---

## Connection Management & Concurrency

The API manages RFID reader connections in memory through [`RfidConnectionManager`](Managers/RFIDConnectionManager.cs) using thread-safe collections (`ConcurrentDictionary<string, RfidConnection>`):

1. **Per-Client Exclusivity**: A client identified by its remote IP can only have one active connection open at any given time (`IsClientConnected`).
2. **Per-Device Exclusivity**: A physical RFID device identified by `ip:port` can only be held by a single connection session at a time (`IsDeviceInUse`).
3. **Session Lifecycle**: Each successful connection receives a unique GUID `connectionId`. All subsequent tag and workflow requests must supply this `connectionId`.
4. **Cleanup**: Calling the `/api/uhf/disconnect` endpoint releases the hardware TCP socket and purges the connection session from memory.

---

## HTTP Testing

A REST Client request file is available in the repository root:

- [`RFIDAPI.http`](RFIDAPI.http): Contains requests for obtaining an IAM token and calling the API. Review the OAuth, reader, and connection values and replace them with values for your environment before use.

Run the requests in Visual Studio or Visual Studio Code with the **REST Client** extension.
