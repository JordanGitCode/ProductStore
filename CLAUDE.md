# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Golden rule: KEEP IT SIMPLE

This rule outranks everything else in this file. Do the simplest thing that works. Do not add abstractions, patterns, or dependencies beyond what the task in front of you needs — no repository layer over EF, no mapping library, no error handling for cases that cannot happen, no interface with one implementation and no second one planned. Do not refactor or tidy code the task did not ask you to touch.

If a task seems to genuinely need a new pattern or dependency, say so and ask. Do not build it and explain afterwards.

## Goal

ProductStore is a product management application. A user photographs an item, uploads the photos to create a listing, and the app scans those photos and returns suggested values for the listing's fields — the user can accept or edit any of them. Listings are browsed through a catalog view with sorting and filtering.

The core flow to build, in order:

1. **Upload** — accept one or more photos for a product and create a listing from them.
2. **Suggest** — scan the uploaded photos and return suggested field data (name, description, category, price). Suggestions are advisory; the user always remains able to change every field.
3. **Edit** — full CRUD on a listing and its data.
4. **Catalog** — list view with sorting and filtering.

**Auth is deliberately deferred** until the above works end to end. Do not add authentication, user accounts, or per-user ownership unless asked. Note that listings are implicitly user-owned in the final product, so avoid designs that would make retrofitting an owner relationship painful.

## Decided vs. open

Fixed: the API is C# / .NET, and the frontend is Angular. Hosting is still open — present options rather than assuming a choice, and do not silently introduce a dependency that settles it.

The frontend does not exist in this repo yet.

## Image storage

**Decision: photo bytes live in SQL Server (`varbinary(max)`) for now.** Chosen because it adds no infrastructure, no storage config, no orphaned-file cleanup, and is covered by the existing database backup — the simplest thing that works at this stage.

The known tradeoff is accepted deliberately: this bloats the database and loads whole images into memory per request. It is fine at dev scale and wrong at real scale. Moving to blob storage later means changing where bytes are read and written, not the shape of the listing model — so do not pre-build a storage abstraction for a migration that has not been scheduled.

## Photo scanning

**Decision: start with a free, locally-run vision model (Ollama), behind an interface, and upgrade later if quality demands it.** The cost of a hosted model is negligible during development; the reason to start local is to avoid an external dependency and an API key before the flow works end to end. The interface is what makes the upgrade cheap, so it matters more than the initial implementation.

Scanning goes behind `Services/IProductScanner.cs`, registered in DI alongside `IProductService`:

```csharp
public interface IProductScanner
{
    Task<ScanSuggestion> ScanAsync(IReadOnlyList<ProductImage> images, CancellationToken ct);
}
```

Its contract types (`ScanImage`, `ScanSuggestion`) live in `Contracts/`, deliberately not in `Models/` — they are transport types for the scanner, not EF entities. The `Scan` prefix is load-bearing: it keeps them distinct from the `ProductImage` entity that persists photos. `ScanSuggestion` carries suggested values (name, description, category) — never a committed value. Swapping providers should be a one-line change in `Program.cs`; if an implementation's details leak past this interface into the controller or service, that is a design error.

Two constraints this interface exists to protect:

- **Scanning is a background job, not a request.** A scan takes seconds — 30+ on CPU-only Ollama. `POST /product` returns immediately with the listing in a pending state; the result reaches Angular by polling or SSE. Do not make the upload endpoint block on a scan.
- **Price is not derivable from a photo.** Vision can identify a product but not what it is worth. `ScanSuggestion` deliberately omits price; it stays user-entered until there is a pricing source (comparable listings or a product database).

## Current state

The API is an early scaffold: an ASP.NET Core Web API (.NET 10) backed by SQL Server via Entity Framework Core. There is no solution file — `ProductStoreAPI/ProductStoreAPI.csproj` is the only project, and all commands run from the `ProductStoreAPI/` directory.

The read path works end to end: `ProductController` → `IProductService` → `ApplicationDbContext` → SQL Server, with `ProductService` registered in `Program.cs`. `GET /product` and `GET /product/{id:guid}` are live. There is no write path yet — no POST, PUT, or DELETE.

Nothing models photos or persists images. `IProductScanner` and its contract types exist but have **no implementation and no DI registration** — the interface is stubbed, the scanning itself is not built. The `WeatherForecast` template files are still present and can be deleted once real endpoints land.

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

Flow is Controller → Service (`IProductService`) → `ApplicationDbContext` → SQL Server. Services are registered in `Program.cs` and constructor-injected; follow that pattern for new services rather than reaching for `ApplicationDbContext` from a controller.

Entities are returned directly from the controller — there are no DTOs, by design (see the golden rule). Navigation properties are not `Include`d, so `Product.ProductCategory` serializes as `null`. Adding an `Include` will produce a `Product → ProductCategory → Products` cycle that `System.Text.Json` rejects, so the catalog view will need either a projection or `ReferenceHandler` — decide that when the catalog is built, not before.

- **`Program.cs`** — minimal hosting entry point. Registers `ApplicationDbContext` against the `ConnectionStrings:Default` config value, controllers, and OpenAPI. OpenAPI plus the Scalar reference UI (`/scalar/v1`) are mapped only in the Development environment.
- **`Data/ApplicationDbContext.cs`** — exposes `Products` and `ProductCategories`. There is no `OnModelCreating`; the schema comes entirely from convention, so `Product.ProductCategory` produces a required FK with cascade delete, and strings map to `nvarchar(max)`. Add explicit configuration here if you need lengths or different delete behavior.
- **`Controllers/ProjectController.cs`** — note the filename does not match the `ProductController` class inside it. Routes are attribute-based (`[Route("product")]`).

## Local database

The connection string in `appsettings.json` points at `localhost,1433` using `sa` with a committed password and `TrustServerCertificate=True`. This only works against a local/containerized SQL Server. Do not carry this pattern into any non-local configuration — real environments should supply the connection string via user secrets or environment variables.
