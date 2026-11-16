FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
ARG PUBLIC_API_URL=http://localhost:5180
ARG PUBLIC_SITE_URL=http://localhost:4321
ENV PUBLIC_API_URL=$PUBLIC_API_URL
ENV PUBLIC_SITE_URL=$PUBLIC_SITE_URL
RUN npm run build

FROM node:24-alpine AS final
WORKDIR /app

RUN apk add --no-cache curl

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=4321

COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist
COPY --from=build /app/package.json ./

EXPOSE 4321
CMD ["node", "./dist/server/entry.mjs"]
