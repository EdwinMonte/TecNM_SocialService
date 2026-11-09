# Etapa 1: Build de la SPA Vue 3 con pnpm
FROM node:22-alpine AS build
WORKDIR /app/frontend

RUN corepack enable && corepack prepare pnpm@latest --activate

COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml* ./
RUN pnpm install --frozen-lockfile

COPY frontend/ .
COPY backend/Modules/Expedients/social-service-documents.json /app/backend/Modules/Expedients/social-service-documents.json
ENV VITE_API_BASE_URL=/api
RUN pnpm run build

# Etapa 2: Servidor estático Nginx para producción con proxy hacia backend
FROM nginx:alpine AS runtime
COPY --from=build /app/frontend/dist /usr/share/nginx/html
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
