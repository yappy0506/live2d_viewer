using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class LocalApiServer
    {
        private readonly MainThreadDispatcher _dispatcher;
        private readonly Live2DViewerApp _app;
        private readonly AppLogger _logger;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public LocalApiServer(MainThreadDispatcher dispatcher, Live2DViewerApp app, AppLogger logger)
        {
            _dispatcher = dispatcher;
            _app = app;
            _logger = logger;
        }

        public void Start(int port)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
            _logger.Info($"api server started: 127.0.0.1:{port}");
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    Handle(ctx);
                }
                catch (Exception ex)
                {
                    if (_running) _logger.Warn($"api loop: {ex.Message}");
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var path = req.Url?.AbsolutePath ?? "";
            var method = req.HttpMethod;
            var requestId = Guid.NewGuid().ToString();

            try
            {
                if (!_app.Authorize(req))
                {
                    WriteError(ctx, 401, requestId, "E401", "Unauthorized");
                    return;
                }

                if (method == "GET" && path == "/v1/health") { WriteOk(ctx, requestId, _app.GetHealth()); return; }
                if (method == "GET" && path == "/v1/models") { WriteOk(ctx, requestId, _app.GetModelsResponse()); return; }
                if (method == "GET" && path == "/v1/model/status") { WriteOk(ctx, requestId, _app.GetModelStatus()); return; }
                if (method == "GET" && path == "/v1/expressions")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model not ready"); return; }
                    WriteOk(ctx, requestId, _app.GetExpressionsResponse());
                    return;
                }
                if (method == "GET" && path == "/v1/motions")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model not ready"); return; }
                    WriteOk(ctx, requestId, _app.GetMotionsResponse());
                    return;
                }

                var body = ReadBody(req);
                if (method == "POST" && path == "/v1/model/switch")
                {
                    var payload = JsonUtility.FromJson<ModelSwitchRequest>(body);
                    if (payload == null || string.IsNullOrEmpty(payload.model_id)) { WriteError(ctx, 400, requestId, "E100", "invalid request"); return; }
                    if (!_app.HasModel(payload.model_id)) { WriteError(ctx, 200, requestId, "E110", "model not found"); return; }
                    if (!_app.IsModelReady() && !payload.force) { WriteError(ctx, 200, requestId, "E409", "model is loading"); return; }
                    _dispatcher.Enqueue(() => _app.SwitchModelOnMainThread(payload));
                    WriteOk(ctx, requestId, new ModelSwitchStateResponse { state = "loading", model_id = payload.model_id });
                    return;
                }

                if (method == "POST" && path == "/v1/expression/apply")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model not ready"); return; }
                    var payload = JsonUtility.FromJson<ExpressionApplyRequest>(body);
                    if (payload == null || string.IsNullOrEmpty(payload.expression_id)) { WriteError(ctx, 400, requestId, "E100", "invalid request"); return; }
                    _dispatcher.Enqueue(() => _app.HandleExpressionApply(ctx, requestId, payload));
                    return;
                }

                if (method == "POST" && path == "/v1/motion/play")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model not ready"); return; }
                    var payload = JsonUtility.FromJson<MotionPlayRequest>(body);
                    if (payload == null || string.IsNullOrEmpty(payload.motion_id)) { WriteError(ctx, 400, requestId, "E100", "invalid request"); return; }
                    _dispatcher.Enqueue(() => _app.HandleMotionPlay(ctx, requestId, payload));
                    return;
                }

                if (method == "POST" && path == "/v1/motion/stop")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model not ready"); return; }
                    _dispatcher.Enqueue(() => _app.HandleMotionStop(ctx, requestId));
                    return;
                }

                if (method == "POST" && path == "/v1/behavior/auto")
                {
                    var payload = JsonUtility.FromJson<BehaviorAutoRequest>(body);
                    _dispatcher.Enqueue(() => _app.HandleBehavior(ctx, requestId, payload));
                    return;
                }

                if (method == "POST" && path == "/v1/transform")
                {
                    var payload = JsonUtility.FromJson<TransformRequest>(body);
                    _dispatcher.Enqueue(() => _app.HandleTransform(ctx, requestId, payload));
                    return;
                }

                if (method == "POST" && path == "/v1/window/overlay")
                {
                    if (!_app.IsModelReady()) { WriteError(ctx, 200, requestId, "E409", "model is loading"); return; }
                    var payload = JsonUtility.FromJson<OverlayRequest>(body);
                    _dispatcher.Enqueue(() => _app.HandleOverlay(ctx, requestId, payload));
                    return;
                }

                if (method == "POST" && path == "/v1/settings/save")
                {
                    _dispatcher.Enqueue(() => _app.HandleSettingsSave(ctx, requestId));
                    return;
                }

                if (method == "POST" && path == "/v1/settings/load")
                {
                    _dispatcher.Enqueue(() => _app.HandleSettingsLoad(ctx, requestId));
                    return;
                }

                if (method == "POST" && (path == "/v1/lipsync/volume" || path == "/v1/lipsync/viseme"))
                {
                    WriteOk(ctx, requestId, new SimpleAccepted { accepted = true });
                    return;
                }

                WriteNotFound(ctx, requestId);
            }
            catch (Exception ex)
            {
                _logger.Error($"api error: {ex}");
                WriteError(ctx, 500, requestId, "E500", "Internal Error");
            }
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using (var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public static void WriteOk<T>(HttpListenerContext ctx, string requestId, T data)
        {
            var wrap = new ApiResponseOk<T> { ok = true, request_id = requestId, timestamp = DateTimeOffset.Now.ToString("O"), data = data };
            WriteJson(ctx, 200, JsonUtility.ToJson(wrap));
        }

        public static void WriteError(HttpListenerContext ctx, int status, string requestId, string code, string message)
        {
            var wrap = new ApiResponseError
            {
                ok = false,
                request_id = requestId,
                timestamp = DateTimeOffset.Now.ToString("O"),
                error = new ErrorBody { code = code, message = message, details = "" }
            };
            WriteJson(ctx, status, JsonUtility.ToJson(wrap));
        }

        public static void WriteNotFound(HttpListenerContext ctx, string requestId)
        {
            WriteError(ctx, 404, requestId, "E100", "Not Found");
        }

        private static void WriteJson(HttpListenerContext ctx, int status, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Flush();
            ctx.Response.Close();
        }
    }

    [Serializable] public class ApiResponseOk<T> { public bool ok; public string request_id; public string timestamp; public T data; }
    [Serializable] public class ApiResponseError { public bool ok; public string request_id; public string timestamp; public ErrorBody error; }
    [Serializable] public class ErrorBody { public string code; public string message; public string details; }
    [Serializable] public class SimpleAccepted { public bool accepted; }

    [Serializable] public class ModelSwitchRequest { public string model_id; public bool force; }
    [Serializable] public class ExpressionApplyRequest { public string expression_id; public int fade_ms; }
    [Serializable] public class MotionPlayRequest { public string motion_id; public string priority = "mid"; public bool loop; public int fade_ms; }
    [Serializable] public class BehaviorAutoRequest { public bool blink = true; public bool breath = true; public float blink_gain = 1f; public float breath_gain = 1f; }
    [Serializable] public class TransformRequest { public float x; public float y; public float scale = 1f; public string framing = "full"; }
    [Serializable] public class OverlayRequest { public bool transparent = true; public string mode = "chromakey"; public bool always_on_top; public bool click_through; public float opacity = 1f; public string chromakey_color = "#00FF00"; }
    [Serializable] public class ModelSwitchStateResponse { public string state; public string model_id; }
}
