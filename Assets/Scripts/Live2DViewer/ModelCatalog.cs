using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Live2DViewer
{
    public sealed class ModelCatalog
    {
        public List<ModelCatalogItem> Scan()
        {
            var result = new List<ModelCatalogItem>();
            var root = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Live2D");
            if (!Directory.Exists(root)) return result;

            foreach (var dir in Directory.GetDirectories(root))
            {
                var model3 = Directory.GetFiles(dir, "*.model3.json").FirstOrDefault();
                if (string.IsNullOrEmpty(model3)) continue;

                var modelId = Path.GetFileName(dir);
                result.Add(new ModelCatalogItem
                {
                    model_id = modelId,
                    display_name = modelId,
                    model3_path = model3,
                    has_expressions = Directory.GetFiles(dir, "*.exp3.json", SearchOption.AllDirectories).Length > 0,
                    has_motions = Directory.GetFiles(dir, "*.motion3.json", SearchOption.AllDirectories).Length > 0
                });
            }

            return result;
        }
    }
}
