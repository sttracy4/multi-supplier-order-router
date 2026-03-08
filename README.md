# Multi-Supplier Order Router

A containerized REST API that receives multi-item DME (Durable Medical Equipment) orders and routes each item to the optimal supplier based on product capability, geographic coverage, customer satisfaction, and shipment consolidation.

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running)
- Port `8080` and `5432` available on your machine

---

## Starting the App

### 1. Clone / navigate to the project root

```bash
cd multi-supplier-order-router
```

### 2. Create the `.env` file

The repo does not commit credentials. Create a `.env` file in the project root with the following contents — the values can be anything, they just need to be consistent:

```bash
POSTGRES_USER=orderrouter
POSTGRES_PASSWORD=orderrouter_dev_pw
POSTGRES_DB=orderrouter
```

### 3. Start the stack

```bash
docker compose up --build
```

This will:
- Build the .NET 8 API image
- Start a PostgreSQL 16 container
- Run EF Core migrations automatically
- Seed ~1,100 suppliers and ~1,195 products from the `resources/` CSV files
- Start the API on **http://localhost:8080**

First build takes ~60–90 seconds. Subsequent starts are fast (data is already seeded).

### 4. Verify it's running

```bash
curl http://localhost:8080/api/health
# → {"status":"healthy"}
```

### Stopping

```bash
docker compose down          # stop containers, keep data
docker compose down -v       # stop containers + wipe database (re-seeds on next start)
```

---

## Using the UI

Open **http://localhost:8080** in your browser.

![UI layout: left panel for JSON input, right panel for routed response]

### Layout

| Panel | Description |
|---|---|
| **Left — Order JSON** | Paste or type an order in JSON format |
| **Right — Response** | Routed result displayed as supplier cards |

### Sample orders

Three pre-built samples load with one click:

| Button | Scenario |
|---|---|
| **Simple** | Wheelchair + oxygen, NYC ZIP, no mail order |
| **Mail Order** | CPAP + nebulizer, Boston ZIP, mail order enabled |
| **Infeasible** | Remote ZIP with no local suppliers |

### Submitting an order

1. Edit the JSON in the left panel (or click a sample button)
2. Click **Submit** or press `Ctrl+Enter`
3. The right panel shows:
   - **Status badge** — HTTP status + Feasible / Infeasible
   - **Response time**
   - **Supplier cards** — one per shipment, listing each item, category, quantity, and fulfillment mode
   - **Raw JSON** toggle — switch between formatted view and raw JSON
   - **Details > Raw JSON** — expandable raw response at the bottom

### Fulfillment modes

| Mode | Meaning |
|---|---|
| `local` | Supplier serves the customer's ZIP directly |
| `mail_order` | Order ships nationally via a mail-order supplier |

---

## API Reference

Base URL: `http://localhost:8080`

### `POST /api/route`

Route a multi-item order to the optimal supplier(s).

Always returns **HTTP 200**. Check `feasible` in the response body to determine success.

**Request body**

```json
{
  "order_id": "ORD-001",
  "customer_zip": "10015",
  "mail_order": false,
  "items": [
    { "product_code": "WC-STD-001", "quantity": 1 },
    { "product_code": "OX-PORT-024", "quantity": 1 }
  ],
  "priority": "standard",
  "notes": "Optional free-text note"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `order_id` | string | yes | Unique order identifier |
| `customer_zip` | string | yes | 5-digit US ZIP code |
| `mail_order` | boolean | no | `true` = mail-order suppliers eligible; `false` = local only (default) |
| `items` | array | yes | At least one item |
| `items[].product_code` | string | yes | Must exist in the products table |
| `items[].quantity` | integer | yes | Must be ≥ 1 |
| `priority` | string | no | Informational (`"standard"`, `"urgent"`, etc.) |
| `notes` | string | no | Free-text notes |

**Feasible response**

```json
{
  "feasible": true,
  "routing": [
    {
      "supplier_id": "SUP-0636",
      "supplier_name": "Care Supply Corp #636",
      "items": [
        {
          "product_code": "WC-STD-001",
          "quantity": 1,
          "category": "wheelchair",
          "fulfillment_mode": "local"
        }
      ]
    }
  ]
}
```

**Infeasible response** (validation error, no eligible supplier, or internal error)

```json
{
  "feasible": false,
  "errors": [
    "Order must include at least one line item.",
    "Order must include a valid customer_zip."
  ],
  "routing": []
}
```

### Routing priority

1. **Feasibility** — supplier must carry the product category and serve the ZIP (or be mail-order eligible)
2. **Consolidation** — prefer a single supplier to minimise shipments
3. **Quality** — higher `customer_satisfaction_score` wins
4. **Geography** — local (ZIP match) beats mail-order when scores are equal

### `GET /api/health`

```bash
curl http://localhost:8080/api/health
# → {"status":"healthy"}
```

### Swagger UI

Full interactive API docs: **http://localhost:8080/swagger**

---

## Project Structure

```
multi-supplier-order-router/
├── docker-compose.yml          # API + PostgreSQL containers
├── .env                        # DB credentials (not committed — create manually)
├── resources/
│   ├── suppliers.csv           # ~1,100 suppliers
│   ├── products.csv            # ~1,195 products
│   └── sample_orders.json      # Sample order payloads
└── src/
    └── OrderRouter.Api/
        ├── Controllers/        # HTTP endpoints
        ├── Services/           # Routing engine + supplier repository
        ├── Data/               # EF Core DbContext + CSV seeder
        ├── Models/             # Database entities
        ├── DTOs/               # Request / response shapes
        ├── Utilities/          # ZIP range parser
        └── wwwroot/            # Single-page UI (index.html)
```

---

## Running Tests

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on the host (not needed to run the app via Docker).

```bash
cd src/OrderRouter.Api.Tests
dotnet test
```

54 tests covering ZIP range parsing, routing logic, CSV parsing, and full HTTP integration scenarios.
