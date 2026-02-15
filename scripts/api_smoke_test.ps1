param(
  [string]$BaseUrl = "http://127.0.0.1:27182",
  [string]$ModelId = "",
  [int]$RequestTimeoutSec = 15,
  [int]$SwitchRequestTimeoutSec = 120,
  [int]$ModelReadyTimeoutSec = 60,
  [switch]$ForceSwitch = $true,
  [switch]$ContinueOnSwitchTimeout = $true
)

$ErrorActionPreference = "Stop"

function Log($msg) {
  Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"
}

function Request-Json($Method, $Path, $Body = $null, $TimeoutSec = $RequestTimeoutSec) {
  $uri = "$BaseUrl$Path"
  try {
    if ($null -ne $Body) {
      return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $Body -TimeoutSec $TimeoutSec
    }
    return Invoke-RestMethod -Method $Method -Uri $uri -TimeoutSec $TimeoutSec
  }
  catch {
    throw "request timeout/error: $Method $Path (timeout=${TimeoutSec}s). detail=$($_.Exception.Message)"
  }
}

function Assert-Ok($response, $stepName) {
  if (-not $response.ok) {
    $detail = ""
    if ($response.error) {
      $detail = " code=$($response.error.code) message=$($response.error.message)"
    }
    throw "$stepName failed.$detail"
  }
}

function Wait-ModelReady() {
  $deadline = (Get-Date).AddSeconds($ModelReadyTimeoutSec)
  while ((Get-Date) -lt $deadline) {
    $status = Request-Json GET "/v1/model/status"
    Assert-Ok $status "model/status"
    $state = "$($status.data.state)"
    if ($state -eq "ready") {
      return $status
    }
    if ($state -eq "error") {
      throw "model status is error. last_error=$($status.data.last_error)"
    }
    Start-Sleep -Milliseconds 500
  }
  throw "model did not become ready within ${ModelReadyTimeoutSec}s"
}

Log "BASE_URL=$BaseUrl REQUEST_TIMEOUT=${RequestTimeoutSec}s SWITCH_TIMEOUT=${SwitchRequestTimeoutSec}s MODEL_READY_TIMEOUT=${ModelReadyTimeoutSec}s"

Log "1) health"
$health = Request-Json GET "/v1/health"
Assert-Ok $health "health"

Log "2) models"
$models = Request-Json GET "/v1/models"
Assert-Ok $models "models"
if (-not $ModelId -and $models.data.models.Count -gt 0) {
  $ModelId = $models.data.models[0].model_id
}

if ($ModelId) {
  Log "3) model/switch (ModelId=$ModelId)"
  $switchBody = @{ model_id = $ModelId; force = [bool]$ForceSwitch } | ConvertTo-Json -Compress
  try {
    $switch = Request-Json POST "/v1/model/switch" $switchBody $SwitchRequestTimeoutSec
    Assert-Ok $switch "model/switch"
  }
  catch {
    if (-not $ContinueOnSwitchTimeout) { throw }
    Log "   WARN: model/switch がタイムアウトしました。model/status 監視を継続します。detail=$($_.Exception.Message)"
  }

  Log "4) wait model ready"
  $status = Wait-ModelReady
  Log "   state=$($status.data.state)"

  Log "5) expressions"
  $expr = Request-Json GET "/v1/expressions"
  Assert-Ok $expr "expressions"

  Log "6) motions"
  $motions = Request-Json GET "/v1/motions"
  Assert-Ok $motions "motions"
}
else {
  Log "モデルが0件のため model switch 以降をスキップ"
}

Log "7) overlay"
$overlayBody = @{ transparent = $true; mode = "chromakey"; chromakey_color = "#00FF00" } | ConvertTo-Json -Compress
$overlay = Request-Json POST "/v1/window/overlay" $overlayBody
Assert-Ok $overlay "overlay"

Log "8) behavior"
$behaviorBody = @{ blink = $true; breath = $true; blink_gain = 1.0; breath_gain = 1.0 } | ConvertTo-Json -Compress
$behavior = Request-Json POST "/v1/behavior/auto" $behaviorBody
Assert-Ok $behavior "behavior"

Log "9) transform"
$transformBody = @{ x = 0.0; y = -1.2; scale = 1.1; framing = "bustup" } | ConvertTo-Json -Compress
$transform = Request-Json POST "/v1/transform" $transformBody
Assert-Ok $transform "transform"

Log "10) settings/save"
$save = Request-Json POST "/v1/settings/save" "{}"
Assert-Ok $save "settings/save"

Log "11) settings/load"
$load = Request-Json POST "/v1/settings/load" "{}"
Assert-Ok $load "settings/load"

Log "Smoke test completed successfully"
