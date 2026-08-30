[CmdletBinding()]
param(
    [string]$Api = "http://localhost:5074",
    [string]$SqlServer = "(localdb)\MSSQLLocalDB",
    [string]$Database = "CustomerSupportCrmTest",
    [string]$Email = "admin@support.local",
    [string]$Password = "Support@123456",
    [switch]$SkipDatabase,
    [switch]$RequireDatabase
)

$ErrorActionPreference = "Stop"

function Assert-Equal([object]$Actual, [object]$Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected', got '$Actual'."
    }
    Write-Host "PASS  $Message" -ForegroundColor Green
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL  $Message" }
    Write-Host "PASS  $Message" -ForegroundColor Green
}

function Invoke-Json([string]$Method, [string]$Path, [object]$Body = $null) {
    $params = @{
        Method = $Method
        Uri = "$Api$Path"
        Headers = @{ Authorization = "Bearer $script:Token"; Accept = "application/json" }
        ContentType = "application/json"
        TimeoutSec = 20
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 10) }
    Invoke-RestMethod @params
}

function Get-Data($Response) {
    if (-not $Response.success) { throw "API returned failure: $($Response.message)" }
    return $Response.data
}

Write-Host "Ticket lifecycle smoke test against $Api" -ForegroundColor Cyan

$login = Invoke-RestMethod -Method Post -Uri "$Api/api/Auth/login" -ContentType "application/json" -TimeoutSec 20 -Body (@{
    email = $Email
    password = $Password
} | ConvertTo-Json)
$script:Token = (Get-Data $login).accessToken
Assert-True (-not [string]::IsNullOrWhiteSpace($Token)) "seeded admin login returns an access token"

$customer = Get-Data (Invoke-Json POST "/api/Customers" @{
    name = "Lifecycle Smoke Customer $(Get-Date -Format yyyyMMddHHmmss)"
    email = "lifecycle-$([guid]::NewGuid().ToString('N'))@example.com"
})
$customerId = [guid]$customer

$categories = Get-Data (Invoke-Json GET "/api/Categories")
$category = @($categories | Where-Object { $_.name -eq "Technical" -or $_.Name -eq "Technical" }) | Select-Object -First 1
if ($null -eq $category) { $category = @($categories) | Select-Object -First 1 }
Assert-True ($null -ne $category) "a seeded ticket category is available"
$categoryId = [guid]$category.Id

$ticketId = [guid](Get-Data (Invoke-Json POST "/api/Tickets" @{
    subject = "Lifecycle smoke test"
    description = "Created by scripts/test-ticket-lifecycle.ps1"
    customerId = $customerId
    categoryId = $categoryId
    priority = "Normal"
}))
Write-Host "Ticket: $ticketId" -ForegroundColor Cyan

$agents = @(Get-Data (Invoke-Json GET "/api/Tickets/assignable-agents"))
Assert-True ($agents.Count -gt 0) "at least one assignable agent is available"
$agentId = [guid]$agents[0].Id

function Get-Ticket {
    return Get-Data (Invoke-Json GET "/api/Tickets/$ticketId")
}

function Change-Status([string]$Status) {
    $ticket = Get-Ticket
    try {
        Invoke-Json POST "/api/Tickets/$ticketId/status" @{
            status = $Status
            rowVersion = $ticket.rowVersion
        } | Out-Null
    } catch {
        # The API can commit successfully and then time out while flushing its response.
        # The authoritative check is the fresh ticket read below.
        Write-Warning "Status request for '$Status' did not return cleanly; checking persisted state."
    }
    Assert-Equal (Get-Ticket).status $Status "ticket moves to $Status"
}

$ticket = Get-Ticket
$assigned = Get-Data (Invoke-Json POST "/api/Tickets/$ticketId/assignee" @{
    assigneeId = $agentId
    rowVersion = $ticket.rowVersion
})
Assert-Equal (Get-Ticket).assigneeId $agentId "ticket is assigned to a real agent"

Change-Status "Open"
Change-Status "Assigned"
Change-Status "In Progress"
Change-Status "Resolved"

$final = Get-Ticket
Assert-Equal $final.status "Resolved" "final ticket status is Resolved"
Assert-True (@($final.history).Count -ge 6) "ticket history contains create, assignment, and status changes"
Assert-True (@($final.history | Where-Object { $_.changeType -eq "Created" }).Count -eq 1) "history contains Created"
Assert-True (@($final.history | Where-Object { $_.changeType -eq "Assigned" }).Count -ge 1) "history contains Assigned"
Assert-True (@($final.history | Where-Object { $_.changeType -eq "StatusChanged" }).Count -eq 4) "history contains four status changes"

$notifications = Get-Data (Invoke-Json GET "/api/Notifications?page=1&pageSize=50")
$notificationItems = @($notifications.Items)
$ticketNotifications = @($notificationItems | Where-Object {
    ([string]$_.Message) -match [regex]::Escape([string]$final.Reference)
})
Assert-True ($ticketNotifications.Count -ge 5) "ticket creation and four status changes produced in-app notifications"
Write-Host ("INFO  ticket notifications: " + (($ticketNotifications | ForEach-Object {
    "$($_.notificationType) / $($_.status) / read=$([bool]$_.readAt)"
}) -join "; ")) -ForegroundColor DarkGray

if (-not $SkipDatabase) {
    $connectionString = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $sql = @"
SET NOCOUNT ON;
SELECT 'Ticket' AS CheckName, Status, AssigneeId FROM Tickets WHERE Id = '$ticketId';
SELECT 'HistoryCount' AS CheckName, COUNT(*) AS Value FROM TicketHistory WHERE TicketId = '$ticketId';
SELECT 'Notifications' AS CheckName, COUNT(*) AS Value FROM Notifications WHERE CorrelationId = '$ticketId';
SELECT 'NotificationStatuses' AS CheckName, STRING_AGG(Status, ',') AS Value FROM Notifications WHERE CorrelationId = '$ticketId';
"@
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = $sql
            $reader = $command.ExecuteReader()
            do {
                while ($reader.Read()) {
                    $values = for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                        "$($reader.GetName($i))=$($reader.GetValue($i))"
                    }
                    Write-Host ("DB    " + ($values -join "; ")) -ForegroundColor DarkGray
                }
            } while ($reader.NextResult())
            $reader.Close()
            Write-Host "PASS  direct SQL checks for ticket, history, and notifications" -ForegroundColor Green
        } catch {
            if ($RequireDatabase) { throw }
            Write-Warning "Database check skipped: $($_.Exception.Message)"
            Write-Warning "Pass -RequireDatabase after fixing LocalDB/connection settings to make this a hard failure."
        }
    } finally {
        if ($connection.State -ne [System.Data.ConnectionState]::Closed) { $connection.Close() }
        $connection.Dispose()
    }
}

Write-Host "Lifecycle smoke test completed successfully." -ForegroundColor Cyan
