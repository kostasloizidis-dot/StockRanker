$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Stop-PortOwner {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique

    if (-not $processIds) {
        Write-Host "No process is listening on port $Port."
        return
    }

    foreach ($processId in $processIds) {
        if ($processId -and $processId -ne $PID) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) {
                Write-Host "Stopping process $($process.Id) on port $Port ($($process.ProcessName))"
                Stop-Process -Id $process.Id -Force
            }
            else {
                Write-Host "Process $processId not found; skipping."
            }
        }
    }
}

Push-Location $Root
try {
    Stop-PortOwner -Port 5139
    Stop-PortOwner -Port 5169
    Write-Host "Stop script completed."
}
finally {
    Pop-Location
}
