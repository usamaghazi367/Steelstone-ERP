# ConstFire

Fire safety / construction management app.

**Phase 1:** Login API + Vue web + Flutter mobile — all integrated with the backend.

See [PROJECT_BRIEF.md](./PROJECT_BRIEF.md) for full requirements.

## Structure

```
ConstFire/
  ConstFire.Backend/    ASP.NET Core 10 API (EF Core + SQL Server + JWT)
  ConstFire.Frontend/   Vue 3 + TypeScript
  ConstFire.Mobile/     Flutter
```

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (included with Visual Studio) or SQL Express
- Node.js 18+
- Flutter SDK (for mobile)

## Default test user

| Field    | Value                 |
|----------|-----------------------|
| Email    | admin@constfire.com   |
| Password | Admin@123             |

Seeded automatically on first API run.

## Run the API

```powershell
cd ConstFire.Backend
dotnet run
```

API: http://localhost:5086

Endpoints:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me` (Bearer token)

## Run the Vue web app

```powershell
cd ConstFire.Frontend
npm install
npm run dev
```

Web: http://localhost:5173

## Run the Flutter mobile app

```powershell
cd ConstFire.Mobile
flutter pub get
flutter run
```

> **Note:** On Android emulator use `http://10.0.2.2:5086` instead of `localhost` in `lib/services/api_service.dart`.

## Tech stack

| Layer  | Stack                                      |
|--------|--------------------------------------------|
| API    | ASP.NET Core 10, EF Core, SQL Server, JWT  |
| Web    | Vue 3, TypeScript, Pinia, Vue Router, Axios|
| Mobile | Flutter, http, shared_preferences          |
