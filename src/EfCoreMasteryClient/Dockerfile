# Stage 1: Build Angular application
FROM node:24-alpine AS build
WORKDIR /app

COPY src/EfCoreMasteryClient/package*.json ./
RUN npm ci

COPY src/EfCoreMasteryClient/ ./
RUN npm run build

# Stage 2: Serve with Nginx
FROM nginx:alpine
COPY --from=build /app/dist/EfCoreMasteryClient/browser /usr/share/nginx/html
COPY src/EfCoreMasteryClient/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
