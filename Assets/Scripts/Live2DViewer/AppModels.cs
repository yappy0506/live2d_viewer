using System;

namespace Live2DViewer
{
    [Serializable]
    public class AppConfig
    {
        public bool api_key_required;
        public string api_key = "";
        public string model_id = "";
        public TransformSettings transform = new TransformSettings();
        public OverlaySettings overlay = OverlaySettings.Default();
        public BehaviorSettings behavior = new BehaviorSettings();
        public string last_expression = "";
        public string last_motion = "";
    }

    [Serializable]
    public class TransformSettings
    {
        public float x;
        public float y;
        public float scale = 1f;
        public string framing = "full";
    }

    [Serializable]
    public class OverlaySettings
    {
        public bool transparent = true;
        public string mode = "chromakey";
        public bool always_on_top;
        public bool click_through;
        public float opacity = 1f;
        public string chromakey_color = "#00FF00";

        public static OverlaySettings Default() => new OverlaySettings();
    }

    [Serializable]
    public class BehaviorSettings
    {
        public bool blink = true;
        public bool breath = true;
        public float blink_gain = 1f;
        public float breath_gain = 1f;
    }

    [Serializable]
    public class ModelCatalogItem
    {
        public string model_id;
        public string display_name;
        public string model3_path;
        public bool has_expressions;
        public bool has_motions;
    }

    [Serializable]
    public class ExpressionItem
    {
        public string expression_id;
        public string group;
    }

    [Serializable]
    public class MotionItem
    {
        public string motion_id;
        public string group;
    }
}
