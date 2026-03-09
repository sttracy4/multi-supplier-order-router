# Project Prompts

## Prompt 1 — 2026-03-07
Create a multi-phase plan to build a containerized API service that can receive a multi-item order and then routes orders to supplier(s) based on the following business rules:
a. Product capabilities: Can the supplier fulfill this product category? 
b. Geographic coverage: Does the supplier serve this ZIP code? (unless mail_order is true) 
c. Mail order eligibility: If order allows mail order, consider suppliers with can_mail_order=y 
d. Customer experience: Minimize number of shipments when possible 
e. Quality considerations: Factor in customer satisfaction scores

Order of routing logic priorities
1. Feasibility: Only route to suppliers who can actually fulfill the items
2. Customer experience: Prefer fewer shipments (consolidate with one supplier
when possible)
3. Quality: When multiple options exist, prefer higher-rated suppliers
4. Geographic preference: Prefer local suppliers over mail-order when ratings are
similar

Note: When an order’s `mail_order` attribute is false, only route to suppliers serving the
customer's ZIP code. When `mail_order` is true, any supplier with can_mail_order? = "y" is
eligible regardless of ZIP code.

Suppliers are listed in resources/suppliers.csv
Products are listed in resources/products.csv
Sample orders are listed in resources/sample_orders.json

In the plan allow me flexibility to have input selection to dictate what technologies to use (i.e. docker, .net, spring-boot, postgreSQL)

There will at least be 4 data structures
1. suppliers
- supplier_id: Unique identifier (e.g., "SUP-001") 
- supplier_name: Business name 
- service_zips: ZIP codes served - can be:  
	o Explicit list: "10001, 10002, 10003" 
	o Range: "10001-10100" 
- product_categories: Comma-separated list of categories they handle 
- customer_satisfaction_score: Rating 1-10, or "no ratings yet" 
- can_mail_order?: "y" or "n" (whether they ship nationally) 

2. products
- product_code
- product_name
- category

3. orders (json example)
{
    "order_id": "ORD-001",
    "customer_zip": "10015",
    "mail_order": false,
    "items": [
      {
        "product_code": "WC-STD-001",
        "quantity": 1
      },
      {
        "product_code": "OX-PORT-024",
        "quantity": 1
      }
    ],
    "priority": "standard",
    "notes": "Simple order - wheelchair + oxygen, should have multiple supplier options in NYC"
}

4. api/route endpoint response
{
 "feasible": true,
 "routing": [
 {
 "supplier_id": "SUP-005",
 "supplier_name": "Respiratory Care Co Co",
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

## Claude prompted technology selections. Tech stack selected: .NET 8 ASP.NET Core, PostgreSQL, Docker Compose, seed DB on startup.
## Claude created a 5 phase plan

## Prompt 2
Proceed and execute plan with accept edits on

## Prompt 3
Docker destkop in now running, execute phase 5

## Prompt 4
Create a minimal UI where I can sumbit/paste a order in json format and display the json response

## Prompt 5
analyze supplers.csv and products.csv and find a scenario where multiple suppliers would be needed to complete an order; supply an example order json.

## Prompt 6
Refactor the api/route endpoint to always return 200, if there is a validation error or exception simply return as feasible set to false. Also add a README file detailing how to start the app with docker and use the front end.

## Prompt 7
Ensure proper guards are set up for bad data and normalize data when reading from products.csv and suppliers.csv, do not fail on bad data simply skip record and move on

## Prompt 8
Let's refactor the unsuccessful routeing response to look more like this
{
   "feasible": false,
   "errors":
   [
   "Order must include at least one line item",
   "Order must include a valid customer_zip"
   ]
}

## Prompt 9
Implement a lazy loading caching mechanism for supplier zip, invalidate for now on app restart but note as something to decide later

## Prompt 10
It looks like if I enter two of the same items on separate objects in the items array in an orders json, it will order them separately instead of combining them with a quantity of 2. This is an edge case, what would it look like to fix

## Prompt 11
What are some other edge cases like we just fixed that could also exist in the business logic or app logic? 