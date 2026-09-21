#!/usr/bin/env bash
# Provisions an API key for one device, the same way a fleet admin would from the dashboard.
# Usage: scripts/provision-device.sh <deviceId> [apiBaseUrl]
set -euo pipefail

DEVICE_ID="${1:?Usage: scripts/provision-device.sh <deviceId> [apiBaseUrl]}"
API_BASE_URL="${2:-http://localhost:5231}"
DEMO_USERNAME="${DEMO_USERNAME:-demo}"
DEMO_PASSWORD="${DEMO_PASSWORD:-DemoPass123!}"

TOKEN=$(curl -sf -X POST "$API_BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$DEMO_USERNAME\",\"password\":\"$DEMO_PASSWORD\"}" | jq -r .token)

if [[ -z "$TOKEN" || "$TOKEN" == "null" ]]; then
  echo "error: login failed. Is the API running at $API_BASE_URL?" >&2
  exit 1
fi

curl -sf -X POST "$API_BASE_URL/api/auth/devices" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"deviceId\":\"$DEVICE_ID\"}"
echo
