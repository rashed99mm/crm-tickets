# CCE Carbon Mock Server - Endpoint Test Script (PowerShell)
# Tests all SMS and Email endpoints

$BASE_URL = "http://localhost:3001"

Write-Host "============================================"
Write-Host "CCE Carbon - Testing Mock Endpoints"
Write-Host "Port: 3001"
Write-Host "============================================"
Write-Host ""

Write-Host "[1/8] Health Check..."
try {
    $response = Invoke-RestMethod -Uri "$BASE_URL/health" -Method GET
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[2/8] Server Info..."
try {
    $response = Invoke-RestMethod -Uri "$BASE_URL/api/server-info" -Method GET
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[3/8] SMS Send (Success)..."
try {
    $body = '{"to":"+966501234567","body":"Welcome to CCE Carbon!"}'
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/sms/send" -Method POST -ContentType "application/json" -Body $body
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[4/8] SMS Send (Failure - Invalid Number)..."
try {
    $body = '{"to":"+966500000000","body":"Test"}'
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/sms/send" -Method POST -ContentType "application/json" -Body $body
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[5/8] SMS Templates..."
try {
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/sms/templates" -Method GET
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[6/8] Email Send (Success)..."
try {
    $body = '{"to":"user@example.com","subject":"Welcome","html":"<h1>CCE Carbon</h1>"}'
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/email/send" -Method POST -ContentType "application/json" -Body $body
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[7/8] Email Send (Failure - Bounce)..."
try {
    $body = '{"to":"bounce@test.com","subject":"Test","html":"<h1>Test</h1>"}'
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/email/send" -Method POST -ContentType "application/json" -Body $body
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "[8/8] Email Templates..."
try {
    $response = Invoke-RestMethod -Uri "$BASE_URL/integrationgateway/email/templates" -Method GET
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "============================================"
Write-Host "All tests completed!"
Write-Host "============================================"
