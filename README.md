# StockRanker

StockRanker ranks tracked S&P 500 companies by how close their current price is to their 6-month low. The solution has two main components:

- `StockRanker.Api`: ASP.NET Core minimal API that exposes stock ranking data.
- `StockRanker.Ui`: Blazor Server UI that displays and refreshes the rankings.

## Project Structure

The solution is split into layered projects. The intended dependency direction is:

`Api` / `Ui` -> `Infrastructure` -> `Application` -> `Domain`

`StockRanker.Domain` contains the core business vocabulary and contracts. It defines records such as `StockCompany`, `StockRanking`, `StockRankingSnapshot`, and `StockDataFetchResult`, plus enums such as `StockRankingLabel` and `StockRefreshStatus`. It also defines interfaces like `IStockPriceProvider` and `IStockRankingCache`, which describe what the application needs without deciding how data is fetched or stored.

`StockRanker.Application` contains the use-case logic. `StockRankingService` gets tracked companies from `IStockPriceProvider`, fetches prices, calculates the ranking score, sorts companies, assigns labels, handles stale cached data when fresh fetches fail, and writes snapshots through `IStockRankingCache`. This layer depends on `Domain`, but not on files, HTTP APIs, ASP.NET, or the UI.

`StockRanker.Infrastructure` contains concrete implementations for external concerns. `FinnhubStockPriceProvider` calls the Finnhub API, `JsonStockPriceProvider` reads seeded stock data from JSON, and `FileStockRankingCache` stores ranking snapshots on disk. This layer implements the interfaces defined in `Domain` and supplies real data/storage behavior to the application.

`StockRanker.Api` is the HTTP entry point. It wires dependency injection, chooses the stock price provider based on configuration, enables Swagger, and exposes endpoints such as `GET /api/stocks/rankings` and `POST /api/stocks/refresh`.

`StockRanker.Ui` is the Blazor Server frontend. It calls the API with `HttpClient`, displays the rankings table, supports company search, and triggers refreshes through the API.

The test projects cover the logic at two levels: `StockRanker.Tests.Unit` tests ranking behavior directly, while `StockRanker.Tests.Integration` tests the API endpoints through an in-memory test host.

## Local Run

The preferred local run command is:

```powershell
.\run.ps1
```

This builds and starts both components.

| Component | Local URL | Functionality |
| --- | --- | --- |
| UI | `http://localhost:5169` | Browser UI for viewing rankings, searching companies, and refreshing data. |
| API | `http://localhost:5139` | API root that lists the available API endpoints. |
| API Swagger | `http://localhost:5139/swagger` | Interactive API documentation and request runner. |

Run without opening the browser:

```powershell
.\run.ps1 -NoBrowser
```

Run without rebuilding first:

```powershell
.\run.ps1 -SkipBuild
```

## Docker Compose Run

Build and run both services with Docker Compose:

```powershell
docker compose up --build
```

Run already-built services:

```powershell
docker compose up
```

| Component | Docker Host URL | Container URL | Functionality |
| --- | --- | --- | --- |
| UI | `http://localhost:5169` | `http://ui:80` | Browser UI for viewing rankings, searching companies, and refreshing data. |
| API | `http://localhost:5139` | `http://api:80` | Stock ranking API. |
| API Swagger | `http://localhost:5139/swagger` | `http://api:80/swagger` | Interactive API documentation and request runner. |

Inside Docker Compose, the UI calls the API through `http://api:80`.

### Docker Compose Command Reference

Use these commands from the repository root: `c:\SB\StockRanker`.

Build all services:
docker compose build

Build only the API service:
docker compose build api

Build only the UI service:
docker compose build ui

Build and start all services:
docker compose up --build

Build and start only the API service:
docker compose up --build api

Build and start only the UI service:
docker compose up --build ui

Build and start only the API service without dependencies:
docker compose up --build --no-deps api

Build and start only the UI service without dependencies:
docker compose up --build --no-deps ui

Run all services without forcing a rebuild:
docker compose up

Run all services in the background:
docker compose up -d

Run only the API service:
docker compose up api

Run only the UI service:
docker compose up ui

Run only the API service without dependencies:
docker compose up --no-deps api

Run only the UI service without dependencies:
docker compose up --no-deps ui

Stop the running Compose services:
docker compose down

## API URLs

These examples use the API port `5139`, which is the same for `run.ps1` and Docker Compose.

| Component | Method | URL | Functionality |
| --- | --- | --- | --- |
| API | `GET` | `http://localhost:5139/` | Shows a small overview of the API and its available endpoints. |
| API | `GET` | `http://localhost:5139/swagger` | Opens Swagger UI. |
| API | `GET` | `http://localhost:5139/api/stocks/rankings` | Returns the latest stock ranking snapshot. If no cache exists, the API creates one. |
| API | `POST` | `http://localhost:5139/api/stocks/refresh` | Refreshes stock data and returns a new ranking snapshot. |

`/api/stocks/refresh` is a `POST` endpoint. It should be called from Swagger, the UI refresh button, or a command such as:

```powershell
Invoke-RestMethod -Method Post http://localhost:5139/api/stocks/refresh
```

## UI Functionality

The UI is available at:

- `http://localhost:5169`

It provides:

- A table of ranked stocks.
- Search by company name.
- Current price, 6-month low, score, label, and status.
- A refresh button that calls `POST /api/stocks/refresh`.

## Port Summary

| Run Mode | UI Port | API Port |
| --- | --- | --- |
| `.\run.ps1` | `5169` | `5139` |
| Docker Compose | `5169` | `5139` |
