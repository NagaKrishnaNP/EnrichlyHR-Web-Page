#!/bin/sh
# Runs automatically at container start (nginx image convention: anything executable in
# /docker-entrypoint.d/ runs before nginx starts). Writes the API base URL into a small env.js
# file the app reads at runtime, so the same built image can point at different backends
# without a rebuild - set API_BASE_URL when running the container.
set -e

API_BASE_URL="${API_BASE_URL:-http://localhost:5000}"

cat > /usr/share/nginx/html/env.js <<EOF
window.__env = { apiBaseUrl: "${API_BASE_URL}/api" };
EOF

echo "Configured frontend to call API at ${API_BASE_URL}/api"
