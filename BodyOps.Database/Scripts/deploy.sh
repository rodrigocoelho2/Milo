#!/bin/bash
# Publishes BodyOps.Database.dacpac to the SQL Server container. SQL Server takes a few seconds to accept
# connections after it starts, so the publish is retried for up to two minutes.
set -u
: "${SQL_SERVER:=sqlserver}"
: "${SQL_DATABASE:=BodyOps}"
: "${SQL_USER:=sa}"
: "${SQL_PASSWORD:?SQL_PASSWORD is required}"

for attempt in $(seq 1 24); do
  if sqlpackage /Action:Publish \
      /SourceFile:/deploy/BodyOps.Database.dacpac \
      /TargetServerName:"$SQL_SERVER" /TargetDatabaseName:"$SQL_DATABASE" \
      /TargetUser:"$SQL_USER" /TargetPassword:"$SQL_PASSWORD" /TargetTrustServerCertificate:True \
      /p:BlockOnPossibleDataLoss=True; then
    echo "BodyOps database is up to date."
    exit 0
  fi
  echo "SQL Server not ready yet (attempt $attempt), retrying in 5 s…"
  sleep 5
done

echo "Could not publish the database." >&2
exit 1
