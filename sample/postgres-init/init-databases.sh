#!/bin/bash
set -e

# Creates every database listed in POSTGRES_MULTIPLE_DATABASES (comma separated), as set in docker-compose.yml.
# Service4 and Service5 also create their database themselves on first start (EnsureCreated).
for database in $(echo "$POSTGRES_MULTIPLE_DATABASES" | tr ',' ' '); do
    echo "Creating database $database"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
        CREATE DATABASE "$database";
        GRANT ALL PRIVILEGES ON DATABASE "$database" TO $POSTGRES_USER;
EOSQL
done

echo "All databases created successfully"
