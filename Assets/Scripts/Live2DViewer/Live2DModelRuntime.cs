using System;
using System.Collections.Generic;
using System.IO;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Framework.Motion;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class Live2DModelRuntime
    {
        private readonly AppLogger _logger;
        private GameObject _root;
        private CubismModel _model;
        private CubismExpressionController _expressionController;
        private CubismMotionController _motionController;
        private CubismAutoEyeBlinkInput _autoBlink;
        private readonly Dictionary<string, CubismExpressionData> _expressions = new Dictionary<string, CubismExpressionData>();
        private readonly Dictionary<string, AnimationClip> _motions = new Dictionary<string, AnimationClip>();
        private readonly Dictionary<string, string> _motionGroupMap = new Dictionary<string, string>();

        public string State { get; private set; } = "ready";
        public string CurrentModelId { get; private set; } = "";
        public string LastError { get; private set; }

        private BehaviorSettings _behavior = new BehaviorSettings();
        private TransformSettings _transform = new TransformSettings();
        private float _breathTime;

        public Live2DModelRuntime(AppLogger logger)
        {
            _logger = logger;
        }

        public IReadOnlyDictionary<string, CubismExpressionData> Expressions => _expressions;
        public IReadOnlyDictionary<string, AnimationClip> Motions => _motions;
        public IReadOnlyDictionary<string, string> MotionGroups => _motionGroupMap;

        public bool SwitchModel(ModelCatalogItem item, bool force)
        {
            if (State == "loading" && !force)
            {
                LastError = "busy";
                return false;
            }

            State = "loading";
            LastError = null;
            try
            {
                DestroyCurrent();
                var modelJson = CubismModel3Json.LoadAtPath(item.model3_path, LoadAssetFromFile);
                if (modelJson == null) throw new Exception("model3 load failed");

                _model = modelJson.ToModel();
                _root = _model.gameObject;
                _root.name = $"Live2D_{item.model_id}";

                _expressionController = _root.GetComponent<CubismExpressionController>();
                _motionController = _root.GetComponent<CubismMotionController>();
                _autoBlink = _root.GetComponent<CubismAutoEyeBlinkInput>();

                BuildExpressionMap(modelJson, item.model3_path);
                BuildMotionMap(modelJson, item.model3_path);
                CurrentModelId = item.model_id;
                ApplyTransform(_transform);
                ApplyBehavior(_behavior);
                State = "ready";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                State = "error";
                _logger.Error($"switch model failed: {ex}");
                return false;
            }
        }

        public void Tick()
        {
            if (_model == null || !_behavior.breath) return;

            _breathTime += Time.deltaTime;
            var breath = Mathf.Sin(_breathTime * 1.8f) * 0.25f * Mathf.Max(0.01f, _behavior.breath_gain);
            foreach (var p in _model.Parameters)
            {
                if (p.Id == "ParamBreath")
                {
                    p.Value = Mathf.Clamp(p.DefaultValue + breath, p.MinimumValue, p.MaximumValue);
                }
            }
        }

        public (bool ok, string code) ApplyExpression(string expressionId)
        {
            if (_expressionController == null) return (false, "E409");
            var ids = new List<string>(_expressions.Keys);
            var idx = ids.IndexOf(expressionId);
            if (idx < 0) return (false, "E130");
            _expressionController.CurrentExpressionIndex = idx;
            return (true, "");
        }

        public (bool ok, string code, string playId) PlayMotion(string motionId, string priority, bool loop)
        {
            if (_motionController == null) return (false, "E409", "");
            if (!_motions.TryGetValue(motionId, out var clip)) return (false, "E130", "");

            var p = CubismMotionPriority.PriorityNormal;
            if (priority == "high") p = CubismMotionPriority.PriorityForce;
            else if (priority == "low") p = CubismMotionPriority.PriorityIdle;

            _motionController.PlayAnimation(clip, 0, p, loop);
            return (true, "", Guid.NewGuid().ToString());
        }

        public void StopMotion() => _motionController?.StopAllAnimation();

        public void ApplyBehavior(BehaviorSettings behavior)
        {
            _behavior = behavior ?? new BehaviorSettings();
            if (_autoBlink != null)
            {
                _autoBlink.enabled = _behavior.blink;
                _autoBlink.Timescale = Mathf.Clamp(10f * _behavior.blink_gain, 1f, 20f);
            }
        }

        public void ApplyTransform(TransformSettings transform)
        {
            _transform = transform ?? new TransformSettings();
            if (_root == null) return;
            _root.transform.position = new Vector3(_transform.x, _transform.y, 0f);
            var scale = _transform.scale;
            if (_transform.framing == "bustup") scale *= 1.3f;
            _root.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public List<ExpressionItem> GetExpressions()
        {
            var list = new List<ExpressionItem>();
            foreach (var kv in _expressions)
            {
                list.Add(new ExpressionItem { expression_id = kv.Key, group = "default" });
            }

            return list;
        }

        public List<MotionItem> GetMotions()
        {
            var list = new List<MotionItem>();
            foreach (var kv in _motions)
            {
                list.Add(new MotionItem { motion_id = kv.Key, group = _motionGroupMap[kv.Key] });
            }

            return list;
        }

        private void BuildExpressionMap(CubismModel3Json json, string model3Path)
        {
            _expressions.Clear();
            if (_expressionController == null) return;

            var dataList = new List<CubismExpressionData>();
            var baseDir = Path.GetDirectoryName(model3Path) ?? "";
            var exprRefs = json.FileReferences.Expressions;
            if (exprRefs == null)
            {
                var exprListEmpty = ScriptableObject.CreateInstance<CubismExpressionList>();
                exprListEmpty.CubismExpressionObjects = new CubismExpressionData[0];
                _expressionController.ExpressionsList = exprListEmpty;
                return;
            }

            foreach (var exp in exprRefs)
            {
                var expPath = Path.Combine(baseDir, exp.File);
                var expJson = CubismExp3Json.LoadFrom(File.ReadAllText(expPath));
                var data = CubismExpressionData.CreateInstance(expJson);
                var id = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(exp.File));
                _expressions[id] = data;
                dataList.Add(data);
            }

            var exprList = ScriptableObject.CreateInstance<CubismExpressionList>();
            exprList.CubismExpressionObjects = dataList.ToArray();
            _expressionController.ExpressionsList = exprList;
        }

        private void BuildMotionMap(CubismModel3Json json, string model3Path)
        {
            _motions.Clear();
            _motionGroupMap.Clear();
            var baseDir = Path.GetDirectoryName(model3Path) ?? "";
            var groups = json.FileReferences.Motions.GroupNames;
            var motionGroups = json.FileReferences.Motions.Motions;
            if (groups == null || motionGroups == null) return;

            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                var items = motionGroups[i];
                foreach (var motion in items)
                {
                    var path = Path.Combine(baseDir, motion.File);
                    var motionJson = CubismMotion3Json.LoadFrom(File.ReadAllText(path));
                    var clip = motionJson.ToAnimationClip();
                    clip.name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(motion.File));
                    _motions[clip.name] = clip;
                    _motionGroupMap[clip.name] = group;
                }
            }
        }

        private void DestroyCurrent()
        {
            _expressions.Clear();
            _motions.Clear();
            _motionGroupMap.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _model = null;
            }
        }

        private object LoadAssetFromFile(Type assetType, string assetPath)
        {
            if (assetType == typeof(string)) return File.Exists(assetPath) ? File.ReadAllText(assetPath) : null;
            if (assetType == typeof(byte[])) return File.Exists(assetPath) ? File.ReadAllBytes(assetPath) : null;
            if (assetType == typeof(Texture2D))
            {
                if (!File.Exists(assetPath)) return null;
                var bytes = File.ReadAllBytes(assetPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                tex.name = Path.GetFileNameWithoutExtension(assetPath);
                return tex;
            }

            return null;
        }
    }
}
