# CCE Carbon Mock Server - Backup Script (PowerShell)
# Creates a backup of all mock data

$MOCKS_DIR = "$PSScriptRoot\..\mocks"
$BACKUP_DIR = "$MOCKS_DIR\_backups"

if (!(Test-Path $BACKUP_DIR)) {
    New-Item -ItemType Directory -Path $BACKUP_DIR -Force | Out-Null
}

$timestamp = (Get-Date -Format "yyyy-MM-dd-HH-mm-ss")
$backupName = "backup-$timestamp"
$backupPath = "$BACKUP_DIR\$backupName"

Write-Host "============================================"
Write-Host "CCE Carbon - Mock Data Backup"
Write-Host "============================================"
Write-Host "Creating backup: $backupName"

# Copy all JSON files recursively
function Copy-JsonFiles($source, $dest) {
    if (!(Test-Path $dest)) {
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
    }
    
    Get-ChildItem $source -Recurse | Where-Object { 
        $_.Extension -eq '.json' -and $_.FullName -notlike '*_backups*' 
    } | ForEach-Object {
        $relativePath = $_.FullName.Substring($source.Length + 1)
        $destFile = Join-Path $dest $relativePath
        $destFileDir = Split-Path $destFile -Parent
        
        if (!(Test-Path $destFileDir)) {
            New-Item -ItemType Directory -Path $destFileDir -Force | Out-Null
        }
        
        Copy-Item $_.FullName $destFile
    }
}

Copy-JsonFiles $MOCKS_DIR $backupPath

# Keep only last 10 backups
$backups = Get-ChildItem $BACKUP_DIR -Directory | Sort-Object CreationTime -Descending
if ($backups.Count -gt 10) {
    $backups | Select-Object -Skip 10 | Remove-Item -Recurse -Force
    Write-Host "Cleaned up old backups (keeping last 10)"
}

Write-Host "Backup created: $backupPath"
Write-Host "============================================"
