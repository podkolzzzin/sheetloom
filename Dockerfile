FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS dotnet-build
WORKDIR /source
COPY dotnet/ dotnet/
RUN dotnet restore dotnet/Sheetloom.Service/Sheetloom.Service.csproj --locked-mode \
    && dotnet publish dotnet/Sheetloom.Service/Sheetloom.Service.csproj --no-restore -c Release -o /publish

FROM node:26.12.1-bookworm-slim@sha256:28147155d47dc31abe0a46e0d12b59b2fb3d0a2dcc5dc65fce76a074180b16fe AS node-runtime
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts --no-audit --no-fund

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends fonts-dejavu-core curl tini \
    && rm -rf /var/lib/apt/lists/*
COPY --from=node-runtime /usr/local/bin/node /usr/local/bin/node
WORKDIR /app
COPY package.json package-lock.json ./
COPY --from=node-runtime /app/node_modules /app/node_modules
COPY src/ src/
COPY LICENSE THIRD_PARTY_NOTICES.md ./
COPY licenses/ /app/licenses/
COPY --from=node-runtime /usr/local/LICENSE /app/licenses/Node.LICENSE
COPY --from=dotnet-build /publish /app/renderer
ENV NODE_ENV=production PORT=8080 NODE_PORT=8081 TEMPLATES_DIR=/templates CACHE_DIR=/tmp/sheetloom
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD curl --fail --silent http://127.0.0.1:${PORT}/health || exit 1
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "/app/renderer/Sheetloom.Service.dll"]

ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="Sheetloom" \
      org.opencontainers.image.description="Excel files from Vue templates and JSON data" \
      org.opencontainers.image.source="https://github.com/podkolzzzin/sheetloom" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.revision=$REVISION
