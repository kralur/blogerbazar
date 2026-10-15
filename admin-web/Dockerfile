FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
COPY shared/package.json shared/package.json
COPY admin-web/package.json admin-web/package.json
RUN npm ci --workspace admin-web

COPY shared shared
COPY admin-web admin-web
RUN npm --workspace admin-web run build

FROM nginx:1.27-alpine AS runtime
# Railway sets PORT and API_UPSTREAM; the defaults suit a local docker compose network.
ENV PORT=8080 \
    API_UPSTREAM=http://api:8080 \
    NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1
COPY admin-web/nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY admin-web/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/admin-web/dist /usr/share/nginx/html
EXPOSE 8080
