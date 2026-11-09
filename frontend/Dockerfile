# syntax=docker/dockerfile:1

# ---- deps: install exactly what package-lock.json says ----------------------------------
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# ---- test: `docker compose -f docker-compose.test.yml run frontend-tests` -------------------
FROM deps AS test
COPY . .
ENV CI=true
CMD ["npx", "vitest", "run"]

# ---- build: type-check and bundle -----------------------------------------------------------
FROM deps AS build
COPY . .
RUN npm run build

# ---- runtime (default, final stage): static files served by nginx ---------------------------
FROM nginx:stable-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-write-config.sh /docker-entrypoint.d/40-write-config.sh
RUN chmod +x /docker-entrypoint.d/40-write-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
ENV API_BASE_URL=http://localhost:8080
EXPOSE 80
