# Toolify Automation Dashboard

**Angular + .NET 8/9 + SQL Server (Dapper)**  
Import Trello CSV → view KPIs, filterable reports, CSV exports. Includes Employee Summary and a Power BI Reports directory.

![stack](https://img.shields.io/badge/stack-Angular%20%7C%20.NET%20%7C%20SQL--Server-blue)
[![CI](../../actions/workflows/ci.yml/badge.svg)](../../actions)

---

## Features

- **Dashboard (main page):** Key KPIs (Total cards, Overdue, Due next 7 days, WIP) + By-Status breakdown.
- **Filters:** Board ID, Status, Date range (from/to) to narrow graphs/reports.
- **Separate Pages:**
  - **Reports (Cards deep-dive)** with paging & CSV download.
  - **Power BI Reports** directory (List + Details screens).
  - **Employees** (Employee List + Employee Summary counts).
- **Data Import:** Trello CSV upload (Board/List/Card/Status/Dates/Hours/Tags/Members).
- **Downloadable Data:** CSV export for any filtered cards set.
- **(Optional) User Access Control:** API is ready to add JWT + role gating; UI prepared to hide/show sections by role.

---

## Tech Stack

**Frontend:** Angular 15+, TypeScript, Angular Router, Angular CLI, HttpClient, Bootstrap / Angular Material  
**Backend:** .NET 8/9 Minimal API, C#, REST, Dapper, Swagger (Swashbuckle)  
**Database:** SQL Server 2019+ (LocalDB for dev), SSMS for management  
**DevOps (target):** GitHub (source), AWS CodeBuild/CodeDeploy/CodePipeline, EC2/S3/CloudFront, RDS SQL Server, CloudWatch

---

## Repository Structure
toolify-api/ # .NET Minimal API (Swagger, Dapper)
tool-portal-app/ # Angular app (Dashboard, Reports, Employees, PBI)
db/ # schema.sql + seed.sql for local dev (LocalDB)
docs/ # docs/DB_ACCESS.md (remote DB blocker & infra steps), README-links.md
.github/workflows/ # ci.yml (build API & UI)


## Quickstart (Local Development)

> This runs **everything locally** (no remote DB required) and matches production-ish behavior.

### Prerequisites
- **.NET SDK** 9.0+ (8.0+ also OK if your csproj targets net8.0)
- **Node.js** 20.x and **Angular CLI** (`npm i -g @angular/cli`)
- **SQL Server LocalDB** (installed with Visual Studio; or SQL Server Express LocalDB)

### 1) Create LocalDB + Schema + Seed

```bash
# Create & start a LocalDB instance
sqllocaldb create "ToolifyLocal"
sqllocaldb start "ToolifyLocal"

# Prepare schema & seed (files in /db)
sqlcmd -S "(localdb)\ToolifyLocal" -i .\db\schema.sql
sqlcmd -S "(localdb)\ToolifyLocal" -d toolify -i .\db\seed.sql

# Sanity check
sqlcmd -S "(localdb)\ToolifyLocal" -d toolify -Q "SELECT COUNT(*) AS employees FROM dbo.employees"

2) Run the API (http://localhost:5130)
cd toolify-api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Sql" "Server=(localdb)\\ToolifyLocal;Database=toolify;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --urls http://localhost:5130

# Swagger: http://localhost:5130/swagger
PI Endpoints

GET /db/ping — DB health

POST /import/trello — multipart file file (Trello CSV)

GET /reports/dashboard — KPIs + By Status (filters: boardId, status, from, to)

GET /reports/cards — paged deep-dive (filters + page, pageSize)

GET /exports/cards.csv — CSV of filtered cards

GET/POST/PUT/DELETE /pbi-reports — Power BI directory CRUD

3) Run the Angular UI (http://localhost:4200)
cd tool-portal-app
npm install
npm run start  # opens http://localhost:4200
UI Pages

/dashboard — filters + KPIs + By Status + Download CSV

/reports — paged cards table (uses same filters)

/employees — “Employee List” title, clean grid (no blank column), Employee Summary (counts by status)

/pbi — Power BI Report List; /pbi/new — create new PBI entry; /pbi/:id — details/edit

