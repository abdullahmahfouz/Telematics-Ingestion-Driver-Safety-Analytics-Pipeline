# syntax=docker/dockerfile:1
# Dev-mode container: runs the same Vite dev server as `npm run dev`, not a production
# build -- this repo's Docker setup is for "clone and run the whole stack" locally, not
# deploying a static bundle.
FROM node:20-alpine
WORKDIR /app

COPY client/package*.json ./
RUN npm install

COPY client/ .

EXPOSE 5173
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
