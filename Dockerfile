FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /source
COPY dotnet/ dotnet/
RUN dotnet restore dotnet/Sheetloom.Service/Sheetloom.Service.csproj --locked-mode \
    && dotnet publish dotnet/Sheetloom.Service/Sheetloom.Service.csproj --no-restore -c Release -o /publish

FROM node:24-bookworm-slim AS node-runtime

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core curl tini \
    && rm -rf /var/lib/apt/lists/*
COPY --from=node-runtime /usr/local/bin/node /usr/local/bin/node
COPY --from=node-runtime /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts --no-audit --no-fund && npm cache clean --force
COPY src/ src/
COPY LICENSE THIRD_PARTY_NOTICES.md ./
COPY --from=dotnet-build /publish /app/renderer
ENV NODE_ENV=production PORT=8080 RENDERER_PORT=8081 TEMPLATES_DIR=/templates CACHE_DIR=/tmp/sheetloom
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD curl --fail --silent http://127.0.0.1:${PORT}/health || exit 1
ENTRYPOINT ["/usr/bin/tini", "--", "node", "src/main.js"]
