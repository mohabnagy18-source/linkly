# Linkly — URL Shortener with Click Analytics

A link shortener with per-link click statistics. ASP.NET Core 8 minimal API + SQL Server (EF Core), static JS frontend.

## Live
- API: https://linklymn.runasp.net/api/links
- Short redirect: https://linklymn.runasp.net/r/{code}
- Frontend: (Vercel link in project description)

## Stack
- Backend: ASP.NET Core 8 minimal APIs, EF Core (schema `linkly`), SQL Server
- Frontend: vanilla JS + CSS (`client/`), hosted on Vercel
- Hosting: MonsterASP.NET (API + DB), Vercel (UI)

## API
- `POST /api/links` `{ "url": "https://..." }` → `{ code, url, createdAt, clicks }`
- `GET /r/{code}` → 302 redirect, records the click (UA, referer, time)
- `GET /api/links` → all links with click counts
- `GET /api/links/{code}/stats` → clicks per day + recent click log

## Run locally
```
cd server && dotnet run
cd ../client && open index.html   # set API base in app.js
```
