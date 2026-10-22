# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app
COPY src/frontend/rasid-web/package.json src/frontend/rasid-web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/frontend/rasid-web/ ./
# production | uat
ARG BUILD_CONFIGURATION=production
RUN npx ng build --configuration ${BUILD_CONFIGURATION}

# Unprivileged nginx (runs as non-root, listens on 8080).
FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY deploy/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY deploy/docker/security-headers.inc /etc/nginx/security-headers.inc
COPY --from=build /app/dist/rasid-web/browser /usr/share/nginx/html
EXPOSE 8080
