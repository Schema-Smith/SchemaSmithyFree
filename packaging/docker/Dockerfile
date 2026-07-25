# Distribution image for SchemaQuench (Docker Hub + GHCR).
# Build context = repo root. The arch-specific self-contained binary + its ICU
# libs are pre-staged into publish/linux/<amd64|arm64>/ by docker-publish.sh.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
ARG TARGETARCH
WORKDIR /app
COPY publish/linux/${TARGETARCH}/ .
RUN chown -R ubuntu:ubuntu /app
USER ubuntu
ENTRYPOINT ["./SchemaQuench"]
