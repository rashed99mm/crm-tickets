#!/bin/bash

# CCE Carbon Mock Server - Endpoint Test Script (Bash)
# Tests all SMS and Email endpoints

BASE_URL="http://localhost:3001"

echo "============================================"
echo "CCE Carbon - Testing Mock Endpoints"
echo "Port: 3001"
echo "============================================"
echo ""

echo "[1/8] Health Check..."
curl -s $BASE_URL/health | jq .
echo ""

echo "[2/8] Server Info..."
curl -s $BASE_URL/api/server-info | jq .
echo ""

echo "[3/8] SMS Send (Success)..."
curl -s -X POST $BASE_URL/integrationgateway/sms/send \
  -H "Content-Type: application/json" \
  -d '{"to":"+966501234567","body":"Welcome to CCE Carbon!"}' | jq .
echo ""

echo "[4/8] SMS Send (Failure - Invalid Number)..."
curl -s -X POST $BASE_URL/integrationgateway/sms/send \
  -H "Content-Type: application/json" \
  -d '{"to":"+966500000000","body":"Test"}' | jq .
echo ""

echo "[5/8] SMS Templates..."
curl -s $BASE_URL/integrationgateway/sms/templates | jq .
echo ""

echo "[6/8] Email Send (Success)..."
curl -s -X POST $BASE_URL/integrationgateway/email/send \
  -H "Content-Type: application/json" \
  -d '{"to":"user@example.com","subject":"Welcome","html":"<h1>CCE Carbon</h1>"}' | jq .
echo ""

echo "[7/8] Email Send (Failure - Bounce)..."
curl -s -X POST $BASE_URL/integrationgateway/email/send \
  -H "Content-Type: application/json" \
  -d '{"to":"bounce@test.com","subject":"Test","html":"<h1>Test</h1>"}' | jq .
echo ""

echo "[8/8] Email Templates..."
curl -s $BASE_URL/integrationgateway/email/templates | jq .
echo ""

echo "============================================"
echo "All tests completed!"
echo "============================================"
