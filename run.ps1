param(
    [switch]$SkipBuild,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ApiProject = Join-Path $Root "StockRanker.Api"
$UiProject = Join-Path $Root "StockRanker.Ui"
$ApiUrl = "http://localhost:5139"
$UiUrl = "http://localhost:5169"

function Stop-PortOwner {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($processId in $processIds) {
        if ($processId -and $processId -ne $PID) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) {
                Write-Host "Stopping process $($process.Id) on port $Port ($($process.ProcessName))"
                Stop-Process -Id $process.Id -Force
            }
        }
    }
}

function Wait-ForUrl {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Url"
}

Push-Location $Root
try {
    if (-not $SkipBuild) {
        Write-Host "Building API..."
        dotnet build ".\StockRanker.Api\StockRanker.Api.csproj" -v:minimal

        Write-Host "Building UI..."
        dotnet build ".\StockRanker.Ui\StockRanker.Ui.csproj" -v:minimal
    }

    Stop-PortOwner -Port 5139
    Stop-PortOwner -Port 5169

    $apiOut = Join-Path $ApiProject "api-run.log"
    $apiErr = Join-Path $ApiProject "api-run.err.log"
    $uiOut = Join-Path $UiProject "ui-run.log"
    $uiErr = Join-Path $UiProject "ui-run.err.log"

    Remove-Item $apiOut, $apiErr, $uiOut, $uiErr -ErrorAction SilentlyContinue

    Write-Host "Starting API on $ApiUrl..."
    $apiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--project", ".\StockRanker.Api.csproj", "--launch-profile", "http", "--no-build") `
        -WorkingDirectory $ApiProject `
        -WindowStyle Hidden `
        -RedirectStandardOutput $apiOut `
        -RedirectStandardError $apiErr `
        -PassThru

    Wait-ForUrl "$ApiUrl/swagger"

    Write-Host "Starting UI on $UiUrl..."
    $uiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--project", ".\StockRanker.Ui.csproj", "--launch-profile", "http", "--no-build") `
        -WorkingDirectory $UiProject `
        -WindowStyle Hidden `
        -RedirectStandardOutput $uiOut `
        -RedirectStandardError $uiErr `
        -PassThru

    Wait-ForUrl $UiUrl

    Write-Host ""
    Write-Host "StockRanker is running."
    Write-Host "UI:          $UiUrl"
    Write-Host "API Swagger: $ApiUrl/swagger"
    Write-Host "API log:     $apiOut"
    Write-Host "UI log:      $uiOut"
    Write-Host "API PID:     $($apiProcess.Id)"
    Write-Host "UI PID:      $($uiProcess.Id)"

    if (-not $NoBrowser) {
        Start-Process $UiUrl
    }
}
finally {
    Pop-Location
}
