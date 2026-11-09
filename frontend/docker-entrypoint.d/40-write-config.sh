#!/bin/sh
# Runs at container start (the official nginx image executes /docker-entrypoint.d/*.sh).
# Writes /config.js so the backend URL can be changed with an environment variable, no rebuild needed.
# The URL is used by the browser, so it must be reachable from the user's machine (not a compose-internal name).
set -eu

: "${API_BASE_URL:=http://localhost:8080}"

# Escape backslashes and double quotes so the value is always a valid JavaScript string.
escaped=$(printf '%s' "$API_BASE_URL" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf 'window.__APP_CONFIG__ = { API_BASE_URL: "%s" };\n' "$escaped" > /usr/share/nginx/html/config.js
echo "40-write-config.sh: API_BASE_URL=$API_BASE_URL"
