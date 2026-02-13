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
