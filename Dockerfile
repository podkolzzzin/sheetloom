FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /source
COPY dotnet/ dotnet/
RUN dotnet restore dotnet/Sheetloom.Service/Sheetloom.Service.csproj --locked-mode \
    && dotnet publish dotnet/Sheetloom.Service/Sheetloom.Service.csproj --no-restore -c Release -o /publish

FROM node:24-bookworm-slim AS node-runtime
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts --no-audit --no-fund

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="Sheetloom" \
      org.opencontainers.image.description="Excel files from Vue templates and JSON data" \
      org.opencontainers.image.source="https://github.com/podkolzzzin/sheetloom" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.revision=$REVISION
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
ENV NODE_ENV=production PORT=8080 RENDERER_PORT=8081 TEMPLATES_DIR=/templates CACHE_DIR=/tmp/sheetloom
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD curl --fail --silent http://127.0.0.1:${PORT}/health || exit 1
ENTRYPOINT ["/usr/bin/tini", "--", "node", "src/main.js"]
