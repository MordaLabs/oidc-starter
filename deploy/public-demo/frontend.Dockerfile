FROM node:22-alpine AS build
WORKDIR /app

COPY src/frontend/package*.json ./
RUN npm ci

COPY src/frontend/ .
ARG FRONTEND_BUILD_CONFIGURATION=public-demo
RUN case "$FRONTEND_BUILD_CONFIGURATION" in \
      public-demo) npm run build:public-demo ;; \
      public-demo-acceptance) npm run build:public-demo-acceptance ;; \
      *) echo "Unsupported frontend build configuration" >&2; exit 2 ;; \
    esac

FROM nginx:1.27-alpine
COPY deploy/public-demo/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser/ /usr/share/nginx/html/

EXPOSE 80
