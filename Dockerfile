FROM node:20-bookworm-slim

# better-sqlite3 ships prebuilt binaries for most platforms; build tools are a
# fallback for architectures (e.g. some ARM NAS boxes) that need compilation.
RUN apt-get update && apt-get install -y --no-install-recommends \
      python3 make g++ ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# install deps first for better layer caching
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund

# app code
COPY server.js ./
COPY public ./public

# state lives here; mount a volume onto it
ENV CORPOOPS_DATA_DIR=/data
RUN mkdir -p /data
VOLUME ["/data"]

EXPOSE 8971

# run as the non-root node user for safety
RUN chown -R node:node /app /data
USER node

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD node -e "require('http').get('http://127.0.0.1:'+(process.env.PORT||8971)+'/api/health',r=>process.exit(r.statusCode===200?0:1)).on('error',()=>process.exit(1))"

CMD ["node", "server.js"]
