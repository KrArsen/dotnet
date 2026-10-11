$ErrorActionPreference = "Stop"

# Значення змінних середовища за замовчуванням (лише для розробки)
$MssqlSaPassword = if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { "LabuStrongPassword2026!" }
$MongoConnString = if ($env:MONGO_CONNECTION_STRING) { $env:MONGO_CONNECTION_STRING } else { "mongodb://localhost:27017/ActivitiesDb" }

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "          РОЗГОРТАННЯ МІКРОСЕРВІСНИХ БАЗ ДАНИХ (CRM ДЛЯ МАЛОГО БІЗНЕСУ)     " -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan

Write-Host "[1/4] Запуск контейнерів SQL Server 2022 та MongoDB 8..." -ForegroundColor Yellow
docker compose up -d --wait
if ($LASTEXITCODE -ne 0) { throw "Помилка запуску docker compose" }

Write-Host "[2/4] Розгортання SalesDb (p1: schema -> procedures -> seed -> security)..." -ForegroundColor Yellow
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p1/schema.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання schema.sql у SalesDb" }

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p1/procedures.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання procedures.sql у SalesDb" }

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p1/seed.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання seed.sql у SalesDb" }

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p1/security.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання security.sql у SalesDb" }

Write-Host "[3/4] Розгортання CatalogDb (p2: schema -> seed -> security)..." -ForegroundColor Yellow
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p2/schema.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання schema.sql у CatalogDb" }

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p2/seed.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання seed.sql у CatalogDb" }

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MssqlSaPassword" -C -b -I -f 65001 -i /scripts/p2/security.sql
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання security.sql у CatalogDb" }

Write-Host "[4/4] Розгортання ActivitiesDb (p3: collections.js -> seed.js)..." -ForegroundColor Yellow
docker compose exec -T mongodb mongosh "$MongoConnString" /scripts/p3/collections.js
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання collections.js у ActivitiesDb" }

docker compose exec -T mongodb mongosh "$MongoConnString" /scripts/p3/seed.js
if ($LASTEXITCODE -ne 0) { throw "Помилка виконання seed.js у ActivitiesDb" }

Write-Host "============================================================================" -ForegroundColor Green
Write-Host "          УСІ БАЗИ ДАНИХ УСПІШНО РОЗГОРНУТО ТА СИДОВАНО!                    " -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Green
