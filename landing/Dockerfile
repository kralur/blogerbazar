FROM node:22-alpine AS build
WORKDIR /app

# The bot's username without "@": every button opens t.me/<bot>. Public, not a secret; Railway passes the
# service variable of the same name as a build argument.
ARG BOT_USERNAME
RUN test -n "$BOT_USERNAME" || (echo "BOT_USERNAME build argument is required" && exit 1)

COPY package.json package-lock.json ./
COPY shared/package.json shared/package.json
COPY landing/package.json landing/package.json
RUN npm ci --workspace landing

COPY shared shared
COPY landing landing
RUN VITE_BOT_USERNAME="$BOT_USERNAME" npm --workspace landing run build

FROM nginx:1.27-alpine AS runtime
ENV PORT=8080
COPY landing/nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY landing/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/landing/dist /usr/share/nginx/html
EXPOSE 8080
