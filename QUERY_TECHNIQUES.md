# Query Techniques: `OUTER APPLY` and `COALESCE`

Notes on two SQL Server constructs used by the sync agent's query executors, why
they were chosen, and how portable they are.

## `OUTER APPLY` — in `GetCustomers`

**The problem:** in AdventureWorks a person can have **several** emails, **several**
phone numbers and **several** addresses. Joining them with a plain `LEFT JOIN` produces
one row per **combination**:

```
Customer 11000 with 2 emails × 3 phones × 2 addresses = 12 rows for a SINGLE customer
```

But the sample payload expects **exactly one row per customer**. That's where
`OUTER APPLY` comes in.

**What it is:** `APPLY` runs a subquery **once for each row** of the outer table, and
that subquery **can reference** the current row (something a normal `JOIN` cannot do).
Combined with `TOP (1)`, it says: *"for this customer, give me at most one email"*.

```sql
OUTER APPLY (
    SELECT TOP (1) ea.EmailAddress
    FROM Person.EmailAddress ea
    WHERE ea.BusinessEntityID = p.BusinessEntityID   -- 👈 references the current row (person p)
    ORDER BY ea.EmailAddressID
) email
```

- **`TOP (1)` + `ORDER BY`** → guarantees exactly one result, and does so
  **deterministically** (always the same one, not an arbitrary pick).
- **`OUTER` APPLY** (vs `CROSS` APPLY) → if the subquery returns nothing (a customer with
  no email), the customer row is **kept** with `NULL` in that field. It behaves like a
  `LEFT JOIN`. `CROSS APPLY` would behave like an `INNER JOIN` and drop the customer with
  no email.

For the address there is also a preference:

```sql
ORDER BY CASE bea.AddressTypeID WHEN 2 THEN 0 ELSE 1 END, bea.AddressID
```

This says: *"if the customer has a Home address (`AddressTypeID = 2`), pick that first;
otherwise, any other."* The `CASE` maps "Home" to `0` (sorts first) and everything else
to `1`.

**In short:** `OUTER APPLY … TOP 1` collapses one-to-many relationships to a single value
per customer, without multiplying rows and without losing customers that have no
email/phone/address.

## `COALESCE` — in `GetOrders`

```sql
COALESCE(p.FirstName + ' ' + p.LastName, s.Name) AS CustomerName
```

**What `COALESCE` does:** returns the **first non-`NULL` argument**. It's a
"whatever is available, in order of preference".

**Why here:** an order belongs to a `Customer`, but that customer can be:
- an **individual** → has a `PersonID` (name in `Person`), or
- a **store** → has a `StoreID` (name in `Sales.Store`).

Because we `LEFT JOIN` both tables, for each order one of them comes back `NULL`.
`COALESCE` resolves it:

1. Try `FirstName + ' ' + LastName` (the person).
2. If that is `NULL` (no person), use `s.Name` (the store name).

> **Note:** in SQL Server, `'text' + NULL` yields `NULL`. So if `p.FirstName` is `NULL`
> because the `LEFT JOIN` to `Person` didn't match, the whole concatenation is `NULL` and
> `COALESCE` cleanly falls through to the store name. Exactly the behaviour we want.

This came out of a finding during validation: the customer on order 43659
(**James Hendergart**) had **both** a `PersonID` **and** a `StoreID`. `COALESCE` with the
person-first order yields the correct person name, which is what the sample payload shows.

## `COALESCE` → standard SQL, portable everywhere

It is part of the ANSI SQL standard (since SQL-92). It works **the same** in SQL Server,
PostgreSQL, MySQL, MariaDB, Oracle, SQLite, DB2… You can use it confidently in any
database.

> Detail: there is an older, less portable proprietary variant — `ISNULL()` in SQL Server,
> `NVL()` in Oracle, `IFNULL()` in MySQL. Those are engine-specific. That's why I used
> `COALESCE` and not `ISNULL`: it works everywhere and also accepts more than two arguments.

## `OUTER APPLY` / `CROSS APPLY` → SQL Server (and Oracle) syntax

`APPLY` is **not** in the standard under that name. It belongs to SQL Server (since 2005)
and Oracle (since 12c). In PostgreSQL, MySQL or SQLite the `APPLY` keyword **does not
exist**.

What **is** standard (since SQL:1999) is the same concept under a different name:
**`LATERAL`**. The mapping is direct:

| SQL Server | Standard / PostgreSQL / MySQL 8+ |
|---|---|
| `CROSS APPLY (…)` | `CROSS JOIN LATERAL (…)` |
| `OUTER APPLY (…)` | `LEFT JOIN LATERAL (…) ON true` |

So my `OUTER APPLY … TOP 1` would be written like this in Postgres:

```sql
LEFT JOIN LATERAL (
    SELECT ea.EmailAddress
    FROM Person.EmailAddress ea
    WHERE ea.BusinessEntityID = p.BusinessEntityID
    ORDER BY ea.EmailAddressID
    LIMIT 1                      -- 👈 LIMIT instead of TOP
) email ON true
```

Note that besides `APPLY` → `LATERAL LEFT JOIN`, `TOP (1)` becomes `LIMIT 1` (another
syntax difference: `TOP` is SQL Server, `LIMIT` is Postgres/MySQL/SQLite, and Oracle uses
`FETCH FIRST n ROWS`).

## Summary and why it matters for the challenge

| Construct | Portable? | Portable equivalent |
|---|---|---|
| `COALESCE` | ✅ All | — |
| `OUTER APPLY` | ❌ SQL Server / Oracle only | `LEFT JOIN LATERAL … ON true` |
| `TOP (n)` | ❌ SQL Server only | `LIMIT n` / `FETCH FIRST` |

For this challenge it is not an issue: the brief explicitly states that clients run
**SQL Server** on-premise, so using SQL Server-specific syntax is correct and idiomatic.
If the agent ever had to support other engines, the query layer (the `Executor` classes)
is exactly where those differences would be isolated — which is why they are encapsulated
one per task type.
