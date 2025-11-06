# Halal-Verifier

A modern .NET library and web API for determining the Halal status of consumer products. Built on ASP.NET Core, this service provides a data-driven answer to the question: *"Is this product Halal?"*



##  Overview
Halal-Verifier provides both a **.NET library** and a **RESTful API** to verify whether a product is Halal based on its ingredients, manufacturer, and certification data. It is designed to be **modular**, **extensible**, and easily integrated into other .NET applications.

---

##  Features
- Verify products via barcode, ingredient list, or manufacturer info.
- RESTful API with endpoints for product verification.
- Modular architecture: separation of models, business logic, services, and API.
- Extensible data models for adding new ingredients or certification rules.
- Can be used in web, desktop, or mobile applications.

---
## Architecture Highlights

| Layer | Purpose |
|------|--------|
| **Model** | Immutable data contracts used across all projects |
| **Database** | Persistence-agnostic with EF Core; seedable via `DbInitializer` |
| **BL** | Stateless verification service with injectable rule sets |
| **ApiService** | Thin API layer exposing BL via Minimal APIs |
| **Web** | Optional SPA/SSR frontend using shared models |
| **ServiceDefaults** | Cross-cutting concerns: logging, validation, CORS |

---
## Tech Stack

- **Runtime**: .NET 8
- **API**: ASP.NET Core Minimal APIs
- **Frontend (Web)**: Blazor Server or Razor Pages
- **ORM**: Entity Framework Core
- **DI**: Microsoft.Extensions.DependencyInjection
- **Validation**: FluentValidation
- **Docs**: Swashbuckle + Redoc
- **Container**: Docker multi-stage

---
