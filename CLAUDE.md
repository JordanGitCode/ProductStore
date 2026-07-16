# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Goal

ProductStore is a product management application. A user photographs an item, uploads the photos to create a listing, and the app scans those photos and returns suggested values for the listing's fields — the user can accept or edit any of them. Listings are browsed through a catalog view with sorting and filtering.

The core flow to build, in order:

1. **Upload** — accept one or more photos for a product and create a listing from them.
2. **Suggest** — scan the uploaded photos and return suggested field data (name, description, category, price). Suggestions are advisory; the user always remains able to change every field.
3. **Edit** — full CRUD on a listing and its data.
4. **Catalog** — list view with sorting and filtering.

**Auth is deliberately deferred** until the above works end to end. Do not add authentication, user accounts, or per-user ownership unless asked. Note that listings are implicitly user-owned in the final product, so avoid designs that would make retrofitting an owner relationship painful.

## Decided vs. open

Fixed: the API is C# / .NET, and the frontend is Angular. Image storage and hosting are still open — present options rather than assuming a choice, and do not silently introduce a dependency that settles one of them.

The frontend does not exist in this repo yet.

## Photo scanning

**Decision: start with a free, locally-run vision model (Ollama), behind an interface, and upgrade later if quality demands it.** The cost of a hosted model is negligible during development; the reason to start local is to avoid an external dependency and an API key before the flow works end to end. The interface is what makes the upgrade cheap, so it matters more than the initial implementation.

Scanning goes behind `Services/IProductScanner.cs`, registered in DI alongside `IProductService`:

```csharp
public interface IProductScanner
{
    Task<ScanSuggestion> ScanAsync(IReadOnlyList<ProductImage> images, CancellationToken ct);
}
```

Its contract types live in `Contracts/`, deliberately not in `Models/` — they are transport types for the scanner, not EF entities, and keeping them out of the model avoids implying a persistence design while image storage is still open. `ScanSuggestion` carries suggested values (name, description, category) — never a committed value. Swapping providers should be a one-line change in `Program.cs`; if an implementation's details leak past this interface into the controller or service, that is a design error.

Two constraints this interface exists to protect:

- **Scanning is a background job, not a request.** A scan takes seconds — 30+ on CPU-only Ollama. `POST /product` returns immediately with the listing in a pending state; the result reaches Angular by polling or SSE. Do not make the upload endpoint block on a scan.
- **Price is not derivable from a photo.** Vision can identify a product but not what it is worth. `ScanSuggestion` deliberately omits price; it stays user-entered until there is a pricing source (comparable listings or a product database).

## Current state

The API is an early scaffold: an ASP.NET Core Web API (.NET 10) backed by SQL Server via Entity Framework Core. There is no solution file — `ProductStoreAPI/ProductStoreAPI.csproj` is the only project, and all commands run from the `ProductStoreAPI/` directory.

`Product`/`ProductCategory` models and the initial migration exist, but `ProductController` returns an empty `Ok()`, `ProductService` is commented out and does not implement `IProductService`, and nothing is registered in DI yet. Nothing models photos or persists images. `IProductScanner` and its contract types exist but have **no implementation and no DI registration** — the interface is stubbed, the scanning itself is not built. The `WeatherForecast` template files are still present and can be deleted once real endpoints land.

## Commands

Run from `ProductStoreAPI/`:

```bash
dotnet build
dotnet run                          # http profile, http://localhost:5100
dotnet run --launch-profile https   # https://localhost:7183
```

There is no test project yet. When adding one, prefer a sibling `ProductStoreAPI.Tests/` project and add a solution file so `dotnet test` covers both.

### Migrations

`dotnet-ef` is pinned to 10.0.10 in a tool manifest, but the manifest sits at `ProductStoreAPI/dotnet-tools.json` rather than the conventional `.config/dotnet-tools.json`, so `dotnet tool restore` will not discover it. Either move it into a `.config/` directory or use a globally installed `dotnet-ef`.

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

## Architecture

Intended flow is Controller → Service (`IProductService`) → `ApplicationDbContext` → SQL Server. To make that real, a service must implement `IProductService`, be registered in `Program.cs` (`builder.Services.AddScoped<IProductService, ProductService>()`), and be injected into the controller — none of which is wired today.

- **`Program.cs`** — minimal hosting entry point. Registers `ApplicationDbContext` against the `ConnectionStrings:Default` config value, controllers, and OpenAPI. OpenAPI plus the Scalar reference UI (`/scalar/v1`) are mapped only in the Development environment.
- **`Data/ApplicationDbContext.cs`** — exposes `Products` and `ProductCategories`. There is no `OnModelCreating`; the schema comes entirely from convention, so `Product.ProductCategory` produces a required FK with cascade delete, and strings map to `nvarchar(max)`. Add explicit configuration here if you need lengths or different delete behavior.
- **`Controllers/ProjectController.cs`** — note the filename does not match the `ProductController` class inside it. Routes are attribute-based (`[Route("product")]`).

## Local database

The connection string in `appsettings.json` points at `localhost,1433` using `sa` with a committed password and `TrustServerCertificate=True`. This only works against a local/containerized SQL Server. Do not carry this pattern into any non-local configuration — real environments should supply the connection string via user secrets or environment variables.
