# README_DEV

## 起動方法
1. Unityで本プロジェクトを開く
2. 任意SceneでPlay（`Live2DViewerApp`が自動生成されます）
3. API: `http://127.0.0.1:27182`

## StreamingAssets/Live2D 配置例
```text
Assets/StreamingAssets/Live2D/
  Hiyori/
    Hiyori.model3.json
    Hiyori.moc3
    textures/
    motions/
    expressions/
```

## API curl例
### health
```bash
curl -s http://127.0.0.1:27182/v1/health
```

### models
```bash
curl -s http://127.0.0.1:27182/v1/models
```

### model switch
```bash
curl -s -X POST http://127.0.0.1:27182/v1/model/switch \
  -H 'Content-Type: application/json' \
  -d '{"model_id":"Hiyori","force":false}'
```

### overlay (chromakey)
```bash
curl -s -X POST http://127.0.0.1:27182/v1/window/overlay \
  -H 'Content-Type: application/json' \
  -d '{"transparent":true,"mode":"chromakey","chromakey_color":"#00FF00"}'
```

### behavior
```bash
curl -s -X POST http://127.0.0.1:27182/v1/behavior/auto \
  -H 'Content-Type: application/json' \
  -d '{"blink":true,"breath":true,"blink_gain":1.0,"breath_gain":1.0}'
```

### settings save/load
```bash
curl -s -X POST http://127.0.0.1:27182/v1/settings/save -H 'Content-Type: application/json' -d '{}'
curl -s -X POST http://127.0.0.1:27182/v1/settings/load -H 'Content-Type: application/json' -d '{}'
```


## スモークテストスクリプト
UnityアプリをPlay中（API起動済み）に実行します。

### Bash
```bash
./scripts/api_smoke_test.sh
# 任意指定
BASE_URL=http://127.0.0.1:27182 MODEL_ID=Hiyori REQUEST_TIMEOUT_SEC=15 SWITCH_REQUEST_TIMEOUT_SEC=120 MODEL_READY_TIMEOUT_SEC=60 FORCE_SWITCH=true CONTINUE_ON_SWITCH_TIMEOUT=true ./scripts/api_smoke_test.sh
```

### PowerShell
```powershell
./scripts/api_smoke_test.ps1
# 任意指定
./scripts/api_smoke_test.ps1 -BaseUrl "http://127.0.0.1:27182" -ModelId "Hiyori" -RequestTimeoutSec 15 -SwitchRequestTimeoutSec 120 -ModelReadyTimeoutSec 60 -ForceSwitch -ContinueOnSwitchTimeout
```


### 補足: モデル切替時のハング対策
- `model/switch` 実行時に応答が返らないケースに備え、両スクリプトはリクエストタイムアウトを持ちます。
- また `model/status` をポーリングし、`ready` になるまで待機（上限時間あり）するため、途中停止原因を特定しやすくしています。


### model/switch がタイムアウトする場合
- モデルが大きい場合は `SwitchRequestTimeoutSec`（PS）/ `SWITCH_REQUEST_TIMEOUT_SEC`（Bash）を増やしてください（例: 180〜300秒）。
- 例: `./api_smoke_test.ps1 -ModelId "IceGirl" -SwitchRequestTimeoutSec 240 -ModelReadyTimeoutSec 300`
- タイムアウト後も `-ContinueOnSwitchTimeout` が有効なら `model/status` の監視を継続し、`ready/error` を判定します。
