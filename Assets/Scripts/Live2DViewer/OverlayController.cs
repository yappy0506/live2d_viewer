using UnityEngine;

namespace Live2DViewer
{
    public sealed class OverlayController
    {
        private readonly Camera _camera;
        private readonly AppLogger _logger;

        public OverlayController(Camera camera, AppLogger logger)
        {
            _camera = camera;
            _logger = logger;
        }

        public (bool ok, string errorCode, string message) Apply(OverlaySettings settings)
        {
            if (settings.mode == "native")
            {
                return (false, "E140", "native mode is unsupported in this build");
            }

            if (!_camera)
            {
                return (false, "E500", "main camera not found");
            }

            if (!ColorUtility.TryParseHtmlString(settings.chromakey_color, out var color))
            {
                color = Color.green;
            }

            _camera.backgroundColor = color;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _logger.Info($"overlay set mode={settings.mode}, chromakey={settings.chromakey_color}, topmost={settings.always_on_top}, click={settings.click_through}, opacity={settings.opacity}");
            return (true, "", "");
        }
    }
}
