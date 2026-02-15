#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:27182}"
MODEL_ID="${MODEL_ID:-}"
REQUEST_TIMEOUT_SEC="${REQUEST_TIMEOUT_SEC:-15}"
MODEL_READY_TIMEOUT_SEC="${MODEL_READY_TIMEOUT_SEC:-60}"
FORCE_SWITCH="${FORCE_SWITCH:-true}"

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
    curl -sS --max-time "$REQUEST_TIMEOUT_SEC" -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body"
  else
    curl -sS --max-time "$REQUEST_TIMEOUT_SEC" -X "$method" "$BASE_URL$path"
  fi
}

assert_ok_true() {
  local json="$1"
  python - "$json" <<'PY'
import json,sys
obj=json.loads(sys.argv[1])
if obj.get('ok') is not True:
    print(json.dumps(obj, ensure_ascii=False))
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

wait_model_ready() {
  local start now elapsed status_json state
  start="$(date +%s)"
  while true; do
    status_json="$(request GET /v1/model/status)" || return 1
    assert_ok_true "$status_json" || return 1
    state="$(extract_data_field "$status_json" state)"
    if [[ "$state" == "ready" ]]; then
      log "   state=ready"
      return 0
    fi
    if [[ "$state" == "error" ]]; then
      local last_error
      last_error="$(extract_data_field "$status_json" last_error)"
      fail "model status error: ${last_error}"
    fi

    now="$(date +%s)"
    elapsed=$((now - start))
    if (( elapsed >= MODEL_READY_TIMEOUT_SEC )); then
      fail "model did not become ready within ${MODEL_READY_TIMEOUT_SEC}s"
    fi
    sleep 0.5
  done
}

log "BASE_URL=$BASE_URL REQUEST_TIMEOUT=${REQUEST_TIMEOUT_SEC}s MODEL_READY_TIMEOUT=${MODEL_READY_TIMEOUT_SEC}s"

log "1) health"
HEALTH_JSON="$(request GET /v1/health)" || fail "health request timeout/error"
assert_ok_true "$HEALTH_JSON" || fail "health check failed"
log "   OK"

log "2) models"
MODELS_JSON="$(request GET /v1/models)" || fail "models request timeout/error"
assert_ok_true "$MODELS_JSON" || fail "models failed"
if [[ -z "$MODEL_ID" ]]; then
  MODEL_ID="$(extract_first_model_id "$MODELS_JSON")"
fi
if [[ -z "$MODEL_ID" ]]; then
  log "   モデルが0件のため model switch 以降をスキップ"
else
  log "   MODEL_ID=$MODEL_ID"

  log "3) model/switch"
  SWITCH_JSON="$(request POST /v1/model/switch "{\"model_id\":\"$MODEL_ID\",\"force\":$FORCE_SWITCH}")" || fail "model/switch request timeout/error"
  assert_ok_true "$SWITCH_JSON" || fail "model switch failed"
  log "   accepted"

  log "4) wait model ready"
  wait_model_ready

  log "5) expressions"
  EXPRESSIONS_JSON="$(request GET /v1/expressions)" || fail "expressions request timeout/error"
  assert_ok_true "$EXPRESSIONS_JSON" || fail "expressions failed"
  log "   OK"

  log "6) motions"
  MOTIONS_JSON="$(request GET /v1/motions)" || fail "motions request timeout/error"
  assert_ok_true "$MOTIONS_JSON" || fail "motions failed"
  log "   OK"
fi

log "7) overlay (chromakey)"
OVERLAY_JSON="$(request POST /v1/window/overlay '{"transparent":true,"mode":"chromakey","chromakey_color":"#00FF00"}')" || fail "overlay request timeout/error"
assert_ok_true "$OVERLAY_JSON" || fail "overlay failed"
log "   OK"

log "8) behavior/auto"
BEHAVIOR_JSON="$(request POST /v1/behavior/auto '{"blink":true,"breath":true,"blink_gain":1.0,"breath_gain":1.0}')" || fail "behavior request timeout/error"
assert_ok_true "$BEHAVIOR_JSON" || fail "behavior failed"
log "   OK"

log "9) transform"
TRANSFORM_JSON="$(request POST /v1/transform '{"x":0.0,"y":-1.2,"scale":1.1,"framing":"bustup"}')" || fail "transform request timeout/error"
assert_ok_true "$TRANSFORM_JSON" || fail "transform failed"
log "   OK"

log "10) settings/save"
SAVE_JSON="$(request POST /v1/settings/save '{}')" || fail "settings/save request timeout/error"
assert_ok_true "$SAVE_JSON" || fail "settings save failed"
log "   OK"

log "11) settings/load"
LOAD_JSON="$(request POST /v1/settings/load '{}')" || fail "settings/load request timeout/error"
assert_ok_true "$LOAD_JSON" || fail "settings load failed"
log "   OK"

log "Smoke test completed successfully"
