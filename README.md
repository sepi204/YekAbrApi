# YekAbr API — Multi-Provider Cloud File Manager

Backend API for a Persian-facing cloud file manager built with **ASP.NET Core 8**, **EF Core**, **PostgreSQL**, and **JWT authentication**.

Users can:

- register / login
- connect **Google Drive**, **Dropbox**, and **MEGA** accounts
- browse, upload, download, create folders, move, rename, and delete files
- run **copy-based transfer jobs** between any connected accounts (including cross-provider)

Internal code identifiers are English. User-facing API messages are Persian.

---

## Solution structure (Clean Architecture)

```
YekAbrApi/
├── README.md
├── YekAbr.sln                    # or YekAbrApi.slnx
└── src/
    ├── YekAbr.Api/               # Controllers, middleware, Swagger, appsettings
    ├── YekAbr.Domain/            # Entities, enums, provider-neutral models, repository interfaces
    ├── YekAbr.Services/          # DTOs, validators, application interfaces
    └── YekAbr.Infrastructure/    # EF Core, Identity, providers, services, DI, migrations
```

| Layer | Responsibility |
|-------|----------------|
| **Api** | Thin controllers, JWT/Swagger setup, exception middleware |
| **Services** | Contracts, DTOs, FluentValidation |
| **Domain** | Entities (`ConnectedCloudAccount`, `CloudTransferJob`), enums, models |
| **Infrastructure** | PostgreSQL + Identity, Google/Dropbox/MEGA providers, encryption, transfer worker |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (local or remote)
- Optional for full cloud features:
  - Google Cloud OAuth client (Drive scope)
  - Dropbox app (App key / secret)
  - A real MEGA account (email/password; MFA optional)

---

## Get the project and run it

### 1) Clone / pull

```powershell
git clone <YOUR_REPO_URL> YekAbrApi
cd YekAbrApi
git pull
```

### 2) Restore packages

```powershell
dotnet restore
```

### 3) Configure `appsettings.json`

Edit `src/YekAbr.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=yekabr_auth_db;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Issuer": "YekAbr.Api",
    "Audience": "YekAbr.Clients",
    "Key": "ChangeThisToAStrongSecretKeyWithAtLeast32Characters",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  },
  "CloudTokenEncryption": {
    "Purpose": "YekAbr.CloudProviderTokens.v1"
  },
  "GoogleDrive": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
    "RedirectUri": "https://localhost:7184/api/cloud/google/callback",
    "Scopes": [
      "openid",
      "email",
      "profile",
      "https://www.googleapis.com/auth/drive"
    ],
    "FrontendSuccessRedirectUrl": "http://localhost:3000/cloud/connect/success",
    "FrontendFailureRedirectUrl": "http://localhost:3000/cloud/connect/failure",
    "OAuthStateLifetimeMinutes": 10
  },
  "Dropbox": {
    "ClientId": "YOUR_DROPBOX_APP_KEY",
    "ClientSecret": "YOUR_DROPBOX_APP_SECRET",
    "RedirectUri": "https://localhost:7184/api/cloud/dropbox/callback",
    "FrontendSuccessRedirectUrl": "http://localhost:3000/cloud/connect/success",
    "FrontendFailureRedirectUrl": "http://localhost:3000/cloud/connect/failure",
    "OAuthStateLifetimeMinutes": 10
  },
  "Mega": {
    "RequestTimeoutSeconds": 120
  }
}
```

**Important**

- Never commit real secrets. Prefer User Secrets / environment variables in real deployments.
- `Jwt:Key` must be a long random secret (≥ 32 characters).
- Google / Dropbox redirect URIs must match the values registered in those developer consoles.
- MEGA does **not** use OAuth client IDs — users connect with email/password (optional MFA) via `POST /api/cloud/mega/connect`.

### 4) Create / update the database

Migrations apply automatically on API startup (`Database.Migrate()` in `Program.cs`).

You can also apply them manually:

```powershell
dotnet ef database update --project src/YekAbr.Infrastructure --startup-project src/YekAbr.Api
```

Ensure PostgreSQL is running and the connection string is correct first.

### 5) Build and run

```powershell
dotnet build
dotnet run --project src/YekAbr.Api
```

Swagger (Development):

- check `src/YekAbr.Api/Properties/launchSettings.json` for the HTTPS port (commonly `https://localhost:7184`)
- open `/swagger`

---

## How authentication works

1. `POST /api/auth/register` — create user (Identity + hashed password)
2. `POST /api/auth/login` — receive JWT access token + refresh token
3. Send `Authorization: Bearer {accessToken}` on protected endpoints
4. `POST /api/auth/refresh` — rotate tokens
5. `POST /api/auth/logout` / `logout-all` — revoke refresh tokens
6. `GET /api/auth/me` — current user profile

In Swagger: click **Authorize** and enter `Bearer {token}`.

---

## How the cloud manager works (end-to-end)

### Provider architecture

All providers implement shared abstractions:

- `ICloudProviderClient` / `ICloudFileProviderClient`
- `ICloudProviderClientFactory` resolves the correct client by `CloudProviderType`
- Tokens / credentials at rest are protected with ASP.NET Data Protection (`ICloudTokenEncryptionService`)
- Shared orchestration:
  - `ICloudAccountService` — list / disconnect / usage
  - `ICloudFileService` — browse / upload / download / CRUD folder ops
  - `ICloudTransferService` + background worker — copy transfers

| Provider | Auth model | Connect entry |
|----------|------------|---------------|
| Google Drive | OAuth 2.0 | `GET /api/cloud/google/connect-url` → `GET /api/cloud/google/callback` |
| Dropbox | OAuth 2.0 | `GET /api/cloud/dropbox/connect-url` → `GET /api/cloud/dropbox/callback` |
| MEGA | Email / password (+ optional MFA) via **MegaApiClient** | `POST /api/cloud/mega/connect` |

Connected accounts are stored in `ConnectedCloudAccount` (provider-neutral). Duplicate reconnect for the same `(UserId, Provider, ProviderAccountId)` updates credentials and reactivates the row.

Disconnect soft-deactivates the account and clears stored credentials (`DELETE /api/cloud/accounts/{id}`).

### Shared file APIs (all providers)

After an account is connected, use the same routes for Google Drive, Dropbox, and MEGA:

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/cloud/accounts` | List connected accounts |
| DELETE | `/api/cloud/accounts/{id}` | Disconnect |
| GET | `/api/cloud/accounts/{id}/usage` | Storage usage |
| GET | `/api/cloud/accounts/{id}/items` | List children (`parentId`, paging, search, filters) |
| GET | `/api/cloud/accounts/{id}/items/{itemId}` | Item details |
| POST | `/api/cloud/accounts/{id}/files/upload` | Multipart upload |
| GET | `/api/cloud/accounts/{id}/files/{itemId}/download` | Stream download |
| DELETE | `/api/cloud/accounts/{id}/items/{itemId}` | Delete |
| POST | `/api/cloud/accounts/{id}/folders` | Create folder |
| POST | `/api/cloud/accounts/{id}/items/{itemId}/move` | Move within same account |
| PATCH | `/api/cloud/accounts/{id}/items/{itemId}/rename` | Rename |

Ownership is always enforced: only the authenticated owner of an active connected account can operate on it.

### Transfer jobs (Phase 6 + MEGA)

Transfers are **copy-based** and provider-neutral (no pair-specific services).

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/cloud/transfers` | Create job |
| GET | `/api/cloud/transfers` | List jobs (filters + paging) |
| GET | `/api/cloud/transfers/{jobId}` | Job details / progress |
| POST | `/api/cloud/transfers/{jobId}/cancel` | Cancel (when supported) |

Create body example:

```json
{
  "sourceConnectedAccountId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "destinationConnectedAccountId": "ffffffff-1111-2222-3333-444444444444",
  "sourceItemId": "provider-specific-item-id",
  "destinationParentFolderId": "optional-destination-folder-id"
}
```

Supported combinations include:

- Google ↔ Dropbox
- Google ↔ MEGA
- Dropbox ↔ MEGA
- MEGA ↔ MEGA (two connected accounts)

A hosted background worker dequeues jobs and streams download → upload through the shared provider clients.

---

## Provider setup notes

### Google Drive

1. Create an OAuth client in Google Cloud Console.
2. Enable Google Drive API.
3. Add authorized redirect URI matching `GoogleDrive:RedirectUri`.
4. Fill `ClientId` / `ClientSecret` in appsettings.
5. Call connect-url while authenticated; complete Google consent; callback stores encrypted tokens.

### Dropbox

1. Create a Dropbox app.
2. Set redirect URI matching `Dropbox:RedirectUri`.
3. Fill app key/secret.
4. Same connect-url + callback pattern as Google.

### MEGA

1. No OAuth app registration is required for this integration.
2. Call authenticated `POST /api/cloud/mega/connect` with:

```json
{
  "email": "user@example.com",
  "password": "****",
  "mfaKey": "optional-if-enabled"
}
```

3. The API generates durable `AuthInfos` via MegaApiClient, encrypts them into `AccessToken`, and never returns password/AuthInfos to the client.
4. Later file/transfer calls decrypt AuthInfos and open a MEGA session per operation.

**Security note:** Prefer HTTPS and treat MEGA passwords carefully on the wire (same as any credential POST). Frontend should not log passwords.

---

## Typical developer workflow

1. Start PostgreSQL and configure connection string.
2. Run the API (`dotnet run --project src/YekAbr.Api`).
3. Register + login in Swagger.
4. Authorize with Bearer token.
5. Connect at least one provider.
6. List accounts → list items → upload/download.
7. Connect a second account and create a transfer job.
8. Poll `GET /api/cloud/transfers/{jobId}` for status/progress.

---

## Response envelope

APIs typically return:

```json
{
  "success": true,
  "message": "پیام فارسی",
  "data": { },
  "errors": null
}
```

Validation / business failures keep messages in Persian for the frontend.

---

## Phases delivered

| Phase | Scope |
|-------|--------|
| 1 | Domain entities/enums, repositories, EF Core + PostgreSQL migrations |
| 2 | Provider abstraction, factory, token encryption |
| 3 | Google Drive connect / accounts / usage |
| 4 | Google Drive file operations via shared routes |
| 5 | Dropbox (same abstraction) |
| 6 | Provider-neutral transfer jobs + worker |
| 7 | MEGA integration + final multi-provider coherence |

---

## Limitations / follow-ups

- MEGA pagination is offset-based in-memory (no native MEGA cursor).
- MEGA MIME types are guessed from file extensions when the provider does not supply them.
- MEGA download keeps a short-lived provider session until the response stream is disposed.
- Google/Dropbox OAuth frontend redirect URLs are optional; if empty, callbacks return JSON instead of redirecting.
- Transfer jobs copy content; they do not move/delete the source.
- No sharing links, version history, webhooks, or scheduled sync in this codebase.

---

## Useful commands cheat sheet

```powershell
# Restore
dotnet restore

# Build
dotnet build

# Run API
dotnet run --project src/YekAbr.Api

# Apply migrations manually
dotnet ef database update --project src/YekAbr.Infrastructure --startup-project src/YekAbr.Api

# Add a new migration (when schema changes)
dotnet ef migrations add MigrationName --project src/YekAbr.Infrastructure --startup-project src/YekAbr.Api
```

---

## License / contribution

Internal project conventions:

- English identifiers in code
- Persian user-facing messages
- Prefer extending shared abstractions over provider-specific parallel APIs
