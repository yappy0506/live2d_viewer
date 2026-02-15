param(
  [string]$BaseUrl = "http://127.0.0.1:27182",
  [string]$ModelId = ""
)

$ErrorActionPreference = "Stop"

function Log($msg) {
  Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"
}

function Request-Json($Method, $Path, $Body = $null) {
  $uri = "$BaseUrl$Path"
  if ($null -ne $Body) {
    return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $Body
  }
  return Invoke-RestMethod -Method $Method -Uri $uri
}

Log "BASE_URL=$BaseUrl"

Log "1) health"
$health = Request-Json GET "/v1/health"
if (-not $health.ok) { throw "health failed" }

Log "2) models"
$models = Request-Json GET "/v1/models"
if (-not $models.ok) { throw "models failed" }
if (-not $ModelId -and $models.data.models.Count -gt 0) {
  $ModelId = $models.data.models[0].model_id
}

if ($ModelId) {
  Log "3) model/switch"
  $switchBody = @{ model_id = $ModelId; force = $false } | ConvertTo-Json -Compress
  $switch = Request-Json POST "/v1/model/switch" $switchBody
  if (-not $switch.ok) { throw "model switch failed" }

  Log "4) model/status"
  $status = Request-Json GET "/v1/model/status"
  if (-not $status.ok) { throw "model status failed" }

  Log "5) expressions"
  $expr = Request-Json GET "/v1/expressions"
  if (-not $expr.ok) { throw "expressions failed" }

  Log "6) motions"
  $motions = Request-Json GET "/v1/motions"
  if (-not $motions.ok) { throw "motions failed" }
}
else {
  Log "モデルが0件のため model switch 以降をスキップ"
}

Log "7) overlay"
$overlayBody = @{ transparent = $true; mode = "chromakey"; chromakey_color = "#00FF00" } | ConvertTo-Json -Compress
$overlay = Request-Json POST "/v1/window/overlay" $overlayBody
if (-not $overlay.ok) { throw "overlay failed" }

Log "8) behavior"
$behaviorBody = @{ blink = $true; breath = $true; blink_gain = 1.0; breath_gain = 1.0 } | ConvertTo-Json -Compress
$behavior = Request-Json POST "/v1/behavior/auto" $behaviorBody
if (-not $behavior.ok) { throw "behavior failed" }

Log "9) transform"
$transformBody = @{ x = 0.0; y = -1.2; scale = 1.1; framing = "bustup" } | ConvertTo-Json -Compress
$transform = Request-Json POST "/v1/transform" $transformBody
if (-not $transform.ok) { throw "transform failed" }

Log "10) settings/save"
$save = Request-Json POST "/v1/settings/save" "{}"
if (-not $save.ok) { throw "settings save failed" }

Log "11) settings/load"
$load = Request-Json POST "/v1/settings/load" "{}"
if (-not $load.ok) { throw "settings load failed" }

Log "Smoke test completed successfully"
