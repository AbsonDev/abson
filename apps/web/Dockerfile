FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

ARG API_URL=https://api.abson.tech
ENV API_URL=$API_URL
ARG PUBLIC_API_URL=https://api.abson.tech
ENV PUBLIC_API_URL=$PUBLIC_API_URL
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=4321

COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./package.json

EXPOSE 4321
CMD ["node", "./dist/server/entry.mjs"]
