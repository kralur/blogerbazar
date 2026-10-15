FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
# The Design System v2 tokens, Tailwind preset and i18n core live in the shared workspace (also used by admin-web).
COPY shared/package.json shared/package.json
COPY frontend/package.json frontend/package.json
RUN npm ci --workspace frontend

COPY shared shared
COPY frontend frontend
ARG VITE_API_URL=""
ENV VITE_API_URL=$VITE_API_URL
RUN npm --workspace frontend run build

FROM nginx:1.27-alpine AS runtime
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/frontend/dist /usr/share/nginx/html
EXPOSE 80
