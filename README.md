# SQL Server Sync Agent — Candidates Challenge

Read **[CHALLENGE_DESCRIPTION.md](CHALLENGE_DESCRIPTION.md)** for the full challenge explanation and what we're looking for.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or Developer Edition)
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms) or another SQL client
- Git

---

## Database Setup — AdventureWorks OLTP

You need to install the AdventureWorks OLTP sample database. Follow these steps:

### 1. Download the backup file

Download **AdventureWorks2025.bak** from Microsoft's GitHub releases:

> https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorks2025.bak

For more details see: https://learn.microsoft.com/en-us/sql/samples/adventureworks-install-configure

### 2. Move the backup file

Copy the `.bak` file to your SQL Server backup directory. The default location is:

```
C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup
```

The path varies depending on your SQL Server version and instance name.

### 3. Restore the database

**Option A: Using SSMS**

1. Open SSMS and connect to your SQL Server instance
2. Right-click **Databases** → **Restore Database...**
3. Select **Device**, click **...**, then **Add**
4. Browse to the `.bak` file and select it
5. Click **OK** to restore

**Option B: Using T-SQL**

```sql
USE [master];
GO
RESTORE DATABASE [AdventureWorks2025]
FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\AdventureWorks2025.bak'
WITH FILE = 1, NOUNLOAD, STATS = 5;
GO
```

Adjust the path to match your environment.

### 4. Verify

Run a quick query to confirm the database is working:

```sql
USE [AdventureWorks2025];
SELECT COUNT(*) FROM Sales.Customer;
```

---

## Running the Test Platform

The **SyncPlatform** app simulates the central platform API that your sync agent will communicate with.

### Build and run

```bash
cd src/SyncPlatform
dotnet build
dotnet run --project SyncPlatform
```

The WPF app will open with:
- **Server** running on `http://localhost:5100`
- **Buttons** to enqueue sync tasks (Customers, Products, Orders, Inventory)
- **Log viewer** showing all HTTP requests and responses

### API details

- **Endpoint:** `http://localhost:5100`
- **API Key:** Include `X-Api-Key: candidate-test-key-2026` in all requests
- **Full API contract:** See [`docs/api-contract.md`](docs/api-contract.md)
- **Sample payloads:** See [`docs/sample-payloads/`](docs/sample-payloads/)

### Quick test with curl

```bash
# Should return 204 (no tasks queued)
curl -H "X-Api-Key: candidate-test-key-2026" http://localhost:5100/api/sync/next-task

# Click a button in the app, then try again — should return 200 with a task
```

---

## Your Task

Build your solution in the `candidate/` directory. See **[CHALLENGE_DESCRIPTION.md](CHALLENGE_DESCRIPTION.md)** for full details.

---

## Submission

1. Create meaningful commits that explain your reasoning
2. Fill in **[CHALLENGE_SUBMISSION.md](CHALLENGE_SUBMISSION.md)** before submitting
