#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:27182}"
MODEL_ID="${MODEL_ID:-}"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"
}

fail() {
  echo "[FAIL] $*" >&2
  exit 1
}

request() {
  local method="$1"
  local path="$2"
  local body="${3:-}"

  if [[ -n "$body" ]]; then
    curl -sS -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body"
  else
    curl -sS -X "$method" "$BASE_URL$path"
  fi
}

assert_ok_true() {
  local json="$1"
  python - "$json" <<'PY'
import json,sys
obj=json.loads(sys.argv[1])
if obj.get('ok') is not True:
    print(obj)
    sys.exit(1)
PY
}

extract_data_field() {
  local json="$1"
  local field="$2"
  python - "$json" "$field" <<'PY'
import json,sys
obj=json.loads(sys.argv[1])
data=obj.get('data',{})
print(data.get(sys.argv[2],""))
PY
}

extract_first_model_id() {
  local json="$1"
  python - "$json" <<'PY'
import json,sys
obj=json.loads(sys.argv[1])
models=obj.get('data',{}).get('models',[])
print(models[0].get('model_id','') if models else '')
PY
}

log "BASE_URL=$BASE_URL"

log "1) health"
HEALTH_JSON="$(request GET /v1/health)"
assert_ok_true "$HEALTH_JSON" || fail "health check failed"
log "   OK"

log "2) models"
MODELS_JSON="$(request GET /v1/models)"
assert_ok_true "$MODELS_JSON" || fail "models failed"
if [[ -z "$MODEL_ID" ]]; then
  MODEL_ID="$(extract_first_model_id "$MODELS_JSON")"
fi
if [[ -z "$MODEL_ID" ]]; then
  log "   モデルが0件のため model switch 以降をスキップ"
else
  log "   MODEL_ID=$MODEL_ID"

  log "3) model/switch"
  SWITCH_JSON="$(request POST /v1/model/switch "{\"model_id\":\"$MODEL_ID\",\"force\":false}")"
  assert_ok_true "$SWITCH_JSON" || fail "model switch failed"
  log "   OK"

  log "4) model/status"
  STATUS_JSON="$(request GET /v1/model/status)"
  assert_ok_true "$STATUS_JSON" || fail "model status failed"
  STATE="$(extract_data_field "$STATUS_JSON" state)"
  log "   state=$STATE"

  log "5) expressions"
  EXPRESSIONS_JSON="$(request GET /v1/expressions)"
  assert_ok_true "$EXPRESSIONS_JSON" || fail "expressions failed"
  log "   OK"

  log "6) motions"
  MOTIONS_JSON="$(request GET /v1/motions)"
  assert_ok_true "$MOTIONS_JSON" || fail "motions failed"
  log "   OK"
fi

log "7) overlay (chromakey)"
OVERLAY_JSON="$(request POST /v1/window/overlay '{"transparent":true,"mode":"chromakey","chromakey_color":"#00FF00"}')"
assert_ok_true "$OVERLAY_JSON" || fail "overlay failed"
log "   OK"

log "8) behavior/auto"
BEHAVIOR_JSON="$(request POST /v1/behavior/auto '{"blink":true,"breath":true,"blink_gain":1.0,"breath_gain":1.0}')"
assert_ok_true "$BEHAVIOR_JSON" || fail "behavior failed"
log "   OK"

log "9) transform"
TRANSFORM_JSON="$(request POST /v1/transform '{"x":0.0,"y":-1.2,"scale":1.1,"framing":"bustup"}')"
assert_ok_true "$TRANSFORM_JSON" || fail "transform failed"
log "   OK"

log "10) settings/save"
SAVE_JSON="$(request POST /v1/settings/save '{}')"
assert_ok_true "$SAVE_JSON" || fail "settings save failed"
log "   OK"

log "11) settings/load"
LOAD_JSON="$(request POST /v1/settings/load '{}')"
assert_ok_true "$LOAD_JSON" || fail "settings load failed"
log "   OK"

log "Smoke test completed successfully"
