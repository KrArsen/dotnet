#!/usr/bin/env bash
set -e

# Значення змінних середовища за замовчуванням (лише для розробки)
MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-LabuStrongPassword2026!}"
MONGO_CONNECTION_STRING="${MONGO_CONNECTION_STRING:-mongodb://localhost:27017/ActivitiesDb}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "============================================================================"
echo "          РОЗГОРТАННЯ МІКРОСЕРВІСНИХ БАЗ ДАНИХ (CRM ДЛЯ МАЛОГО БІЗНЕСУ)     "
echo "============================================================================"

echo "[1/4] Запуск контейнерів SQL Server 2022 та MongoDB 8..."
docker compose up -d --wait

echo "[2/4] Розгортання SalesDb (p1: schema -> procedures -> seed -> security)..."
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p1/schema.sql
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p1/procedures.sql
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p1/seed.sql
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p1/security.sql

echo "[3/4] Розгортання CatalogDb (p2: schema -> seed -> security)..."
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p2/schema.sql
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p2/seed.sql
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -f 65001 -i /scripts/p2/security.sql

echo "[4/4] Розгортання ActivitiesDb (p3: collections.js -> seed.js)..."
docker compose exec -T mongodb mongosh "$MONGO_CONNECTION_STRING" /scripts/p3/collections.js
docker compose exec -T mongodb mongosh "$MONGO_CONNECTION_STRING" /scripts/p3/seed.js

echo "============================================================================"
echo "          УСІ БАЗИ ДАНИХ УСПІШНО РОЗГОРНУТО ТА СИДОВАНО!                    "
echo "============================================================================"
