# Overridable base image -- set MSSQL_IMAGE in .env (passed here as BASE_IMAGE by
# docker-compose). Any Microsoft SQL Server 2019 / 2022 / 2025 Linux image works;
# the full-text-search add-on below auto-detects the matching product version.
ARG BASE_IMAGE=mcr.microsoft.com/mssql/server:2022-latest
FROM ${BASE_IMAGE}

USER root

# Full-text search is used by the AdventureWorks demo package, so install the fts
# add-on. The base image ships only the microsoft-prod apt source, which doesn't
# carry fts, so add the version-matched mssql-server source. Product is derived
# from the Ubuntu release the image is built on (20.04->2019, 22.04->2022,
# 24.04->2025); the signing key (microsoft-prod.gpg) is already present.
RUN set -e; \
    . /etc/os-release; \
    case "$VERSION_ID" in \
      20.04) PROD=2019 ;; \
      22.04) PROD=2022 ;; \
      24.04) PROD=2025 ;; \
      *) echo "unsupported Ubuntu base $VERSION_ID for mssql-server-fts" >&2; exit 1 ;; \
    esac; \
    command -v curl >/dev/null || { apt-get update && apt-get install -y curl; }; \
    curl -fsSL "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/mssql-server-${PROD}.list" \
      -o /etc/apt/sources.list.d/mssql-server.list; \
    apt-get update; \
    apt-get install -y mssql-server-fts; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

WORKDIR /tmp/devdatabase
COPY ./InitializeDatabase.sql ./
COPY ./is-ready.sh ./
COPY ./wait-for-it.sh ./
COPY ./entrypoint.sh ./
COPY ./setup.sh ./

CMD ["/bin/bash", "entrypoint.sh"]
