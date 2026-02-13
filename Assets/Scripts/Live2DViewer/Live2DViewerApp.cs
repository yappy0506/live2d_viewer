using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class Live2DViewerApp : MonoBehaviour
    {
        private AppLogger _logger;
        private SettingsService _settings;
        private MainThreadDispatcher _dispatcher;
        private LocalApiServer _api;
        private ModelCatalog _catalog;
        private Live2DModelRuntime _runtime;
        private OverlayController _overlay;
        private AppConfig _config;
        private List<ModelCatalogItem> _models = new List<ModelCatalogItem>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("Live2DViewerApp");
            DontDestroyOnLoad(go);
            go.AddComponent<Live2DViewerApp>();
        }

        private void Awake()
        {
            _logger = new AppLogger();
            _settings = new SettingsService(_logger);
            _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();
            _catalog = new ModelCatalog();
            _runtime = new Live2DModelRuntime(_logger);
            _overlay = new OverlayController(Camera.main, _logger);
            _config = _settings.LoadOrDefault();
            _models = _catalog.Scan();

            _api = new LocalApiServer(_dispatcher, this, _logger);
            _api.Start(27182);

            ApplyConfig(_config, true);
            _logger.Info("Live2DViewerApp started");
        }

        private void OnDestroy() => _api?.Stop();

        private void Update()
        {
            _runtime.Tick();
        }

        public bool Authorize(HttpListenerRequest req)
        {
            if (!_config.api_key_required) return true;
            return req.Headers["X-Api-Key"] == _config.api_key;
        }

        public HealthResponse GetHealth() => new HealthResponse { status = "ok", version = "0.4.0" };

        public ModelsResponse GetModelsResponse()
        {
            _models = _catalog.Scan();
            return new ModelsResponse { models = _models.ToArray() };
        }

        public ModelStatusResponse GetModelStatus()
        {
            return new ModelStatusResponse { state = _runtime.State, model_id = _runtime.CurrentModelId, last_error = _runtime.LastError };
        }

        public ExpressionsResponse GetExpressionsResponse()
        {
            return new ExpressionsResponse { expressions = _runtime.GetExpressions().ToArray() };
        }

        public MotionsResponse GetMotionsResponse()
        {
            return new MotionsResponse { motions = _runtime.GetMotions().ToArray() };
        }

        public bool IsModelReady() => _runtime.State == "ready";

        public void HandleModelSwitch(HttpListenerContext ctx, string requestId, ModelSwitchRequest req)
        {
            var item = _models.FirstOrDefault(x => x.model_id == req.model_id);
            if (item == null)
            {
                LocalApiServer.WriteError(ctx, 200, requestId, "E110", "model not found");
                return;
            }

            var ok = _runtime.SwitchModel(item, req.force);
            if (!ok)
            {
                LocalApiServer.WriteError(ctx, 200, requestId, _runtime.State == "loading" ? "E409" : "E120", _runtime.LastError ?? "switch failed");
                return;
            }

            _config.model_id = req.model_id;
            LocalApiServer.WriteOk(ctx, requestId, new ModelSwitchResponse { state = "ready", model_id = req.model_id });
        }

        public void HandleExpressionApply(HttpListenerContext ctx, string requestId, ExpressionApplyRequest req)
        {
            var r = _runtime.ApplyExpression(req.expression_id);
            if (!r.ok)
            {
                LocalApiServer.WriteError(ctx, 200, requestId, r.code, "expression apply failed");
                return;
            }

            _config.last_expression = req.expression_id;
            LocalApiServer.WriteOk(ctx, requestId, new ExpressionApplyResponse { applied = true, expression_id = req.expression_id });
        }

        public void HandleMotionPlay(HttpListenerContext ctx, string requestId, MotionPlayRequest req)
        {
            var r = _runtime.PlayMotion(req.motion_id, req.priority ?? "mid", req.loop);
            if (!r.ok)
            {
                LocalApiServer.WriteError(ctx, 200, requestId, r.code, "motion play failed");
                return;
            }

            _config.last_motion = req.motion_id;
            LocalApiServer.WriteOk(ctx, requestId, new MotionPlayResponse { started = true, motion_id = req.motion_id, play_id = r.playId });
        }

        public void HandleMotionStop(HttpListenerContext ctx, string requestId)
        {
            _runtime.StopMotion();
            LocalApiServer.WriteOk(ctx, requestId, new MotionStopResponse { stopped = true });
        }

        public void HandleBehavior(HttpListenerContext ctx, string requestId, BehaviorAutoRequest req)
        {
            _config.behavior = new BehaviorSettings { blink = req.blink, breath = req.breath, blink_gain = req.blink_gain, breath_gain = req.breath_gain };
            _runtime.ApplyBehavior(_config.behavior);
            LocalApiServer.WriteOk(ctx, requestId, _config.behavior);
        }

        public void HandleTransform(HttpListenerContext ctx, string requestId, TransformRequest req)
        {
            _config.transform = new TransformSettings { x = req.x, y = req.y, scale = req.scale, framing = string.IsNullOrEmpty(req.framing) ? "full" : req.framing };
            _runtime.ApplyTransform(_config.transform);
            LocalApiServer.WriteOk(ctx, requestId, _config.transform);
        }

        public void HandleOverlay(HttpListenerContext ctx, string requestId, OverlayRequest req)
        {
            _config.overlay = new OverlaySettings
            {
                transparent = req.transparent,
                mode = req.mode ?? "chromakey",
                always_on_top = req.always_on_top,
                click_through = req.click_through,
                opacity = req.opacity,
                chromakey_color = req.chromakey_color ?? "#00FF00"
            };

            var r = _overlay.Apply(_config.overlay);
            if (!r.ok)
            {
                LocalApiServer.WriteError(ctx, 200, requestId, r.errorCode, r.message);
                return;
            }

            LocalApiServer.WriteOk(ctx, requestId, _config.overlay);
        }

        public void HandleSettingsSave(HttpListenerContext ctx, string requestId)
        {
            _settings.Save(_config);
            LocalApiServer.WriteOk(ctx, requestId, new SettingsSaveResponse { saved = true, path = _settings.ConfigPath });
        }

        public void HandleSettingsLoad(HttpListenerContext ctx, string requestId)
        {
            _config = _settings.LoadOrDefault();
            ApplyConfig(_config, false);
            LocalApiServer.WriteOk(ctx, requestId, new SettingsLoadResponse { loaded = true });
        }

        private void ApplyConfig(AppConfig config, bool withModel)
        {
            _runtime.ApplyBehavior(config.behavior);
            _runtime.ApplyTransform(config.transform);
            _overlay.Apply(config.overlay);
            if (withModel && !string.IsNullOrEmpty(config.model_id))
            {
                var item = _models.FirstOrDefault(x => x.model_id == config.model_id);
                if (item != null)
                {
                    _runtime.SwitchModel(item, true);
                }
            }
        }
    }

    [Serializable] public class HealthResponse { public string status; public string version; }
    [Serializable] public class ModelsResponse { public ModelCatalogItem[] models; }
    [Serializable] public class ModelStatusResponse { public string state; public string model_id; public string last_error; }
    [Serializable] public class ExpressionsResponse { public ExpressionItem[] expressions; }
    [Serializable] public class MotionsResponse { public MotionItem[] motions; }
    [Serializable] public class ModelSwitchResponse { public string state; public string model_id; }
    [Serializable] public class ExpressionApplyResponse { public bool applied; public string expression_id; }
    [Serializable] public class MotionPlayResponse { public bool started; public string motion_id; public string play_id; }
    [Serializable] public class MotionStopResponse { public bool stopped; }
    [Serializable] public class SettingsSaveResponse { public bool saved; public string path; }
    [Serializable] public class SettingsLoadResponse { public bool loaded; }
}
