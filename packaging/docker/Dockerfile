# Distribution image for SchemaQuench (Docker Hub + GHCR).
# Build context = repo root. The arch-specific self-contained binary + its ICU
# libs are pre-staged into publish/linux/<amd64|arm64>/ before the build
# (by docker-selftest.yml for the PR gate; by distribution-publish.yml's docker job
# for real publishes). chown -R makes /app itself writable by the non-root user
# so SchemaQuench can create its run-log directory in the working dir.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
ARG TARGETARCH
WORKDIR /app
COPY publish/linux/${TARGETARCH}/ .
RUN chown -R ubuntu:ubuntu /app
USER ubuntu
ENTRYPOINT ["./SchemaQuench"]
