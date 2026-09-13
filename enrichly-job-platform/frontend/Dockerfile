FROM node:20-alpine AS build
WORKDIR /src

COPY package.json package-lock.json* ./
RUN npm install

COPY . .
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS final
COPY --from=build /src/dist/enrichly-frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.sh /docker-entrypoint.d/40-inject-env.sh
RUN chmod +x /docker-entrypoint.d/40-inject-env.sh

EXPOSE 80
