FROM node:22-alpine AS build
WORKDIR /app

COPY src/frontend/package*.json ./
RUN npm ci

COPY src/frontend/ .
RUN npm run build:public-demo

FROM nginx:1.27-alpine
COPY deploy/public-demo/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser/ /usr/share/nginx/html/

EXPOSE 80
