# ConstFire

Separate project from **HKtab**. Do not mix code or dependencies between the two.

## Goal (Phase 1 — Login only)

Build a complete solution scaffold with working **login** end-to-end:

| Layer | Stack |
|-------|--------|
| API | ASP.NET Core 10, Entity Framework Core, **SQL Server** |
| Web | Vue 3 + TypeScript |
| Mobile | Flutter |

## Backend requirements

- Clean architecture folders: `Controllers`, `Services`, `Data`, `Models`, `DTOs`, `Middleware`
- **Dependency injection** for all services (auth, DbContext, JWT, etc.)
- **Middleware**: exception handling, request logging, JWT auth pipeline
- **SQL Server** connection string in `appsettings.json` (LocalDB or SQL Express for dev)
- EF Core migrations for `Users` table (email, password hash, name, role)
- Auth API: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`
- JWT bearer authentication
- CORS configured for Vue dev server and Flutter

## Frontend requirements

### Vue (ConstFire.Web or ConstFire.Frontend)
- Login page with email/password
- Auth store (token + user in localStorage)
- API client with axios interceptors
- Router guard: redirect to login if not authenticated
- After login → simple dashboard placeholder

### Flutter (ConstFire.Mobile)
- Login screen
- Shared API service pointing to backend
- Token storage (shared_preferences)
- After login → simple home screen placeholder

## Suggested folder layout

```
ConstFire/
  ConstFire.Backend/
  ConstFire.Frontend/
  ConstFire.Mobile/
  README.md
```

## Default test user (seed on first run)

- Email: admin@constfire.com
- Password: Admin@123

## Notes

- Keep HKtab and ConstFire in **separate Cursor chats** and **separate folders**
- This phase stops at login — no other modules yet
