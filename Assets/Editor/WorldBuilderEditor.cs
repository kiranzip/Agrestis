using System.IO;
using UnityEditor;
using UnityEngine;
using Agrestis.Core;
using Agrestis.World;

namespace Agrestis.EditorTools
{
    [CustomEditor(typeof(WorldBuilder))]
    public class WorldBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorldBuilder builder = (WorldBuilder)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) builder.InvalidateSampler();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("World Tools", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Terrain", GUILayout.Height(30)))
                    GenerateTerrain(builder);

                if (GUILayout.Button("Scatter Props", GUILayout.Height(30)))
                    ScatterProps(builder);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Random Seed"))
                {
                    Undo.RecordObject(builder, "New Seed");
                    builder.Seed = Random.Range(1, int.MaxValue);
                    builder.InvalidateSampler();
                    EditorUtility.SetDirty(builder);
                }

                if (GUILayout.Button("Clear Props"))
                    ClearProps(builder);
            }

            EditorGUILayout.Space(6);

            if (builder.TerrainObject == null)
                EditorGUILayout.HelpBox("No terrain generated yet.", MessageType.Warning);
        }

        private static void GenerateTerrain(WorldBuilder builder)
        {
            builder.InvalidateSampler();
            TerrainGenerator generator = builder.Sampler;

            string folder = string.IsNullOrWhiteSpace(builder.MeshSaveFolder)
                ? "Assets/Meshes"
                : builder.MeshSaveFolder.TrimEnd('/');

            Directory.CreateDirectory(folder);
            string meshPath = $"{folder}/Terrain_{builder.gameObject.scene.name}_{builder.Seed}.asset";
            EditorUtility.DisplayProgressBar("Agrestis", "Building terrain mesh...", 0.3f);

            Mesh generated;
            try
            {
                generated = generator.BuildMesh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (asset != null)
            {
                asset.Clear();
                asset.indexFormat = generated.indexFormat;
                asset.vertices = generated.vertices;
                asset.triangles = generated.triangles;
                asset.colors = generated.colors;
                asset.uv = generated.uv;
                asset.RecalculateNormals();
                asset.RecalculateBounds();
                EditorUtility.SetDirty(asset);
                Object.DestroyImmediate(generated);
            }
            else
            {
                AssetDatabase.CreateAsset(generated, meshPath);
                asset = generated;
            }

            AssetDatabase.SaveAssets();

            GameObject terrainGo = builder.TerrainObject;
            if (terrainGo == null)
            {
                terrainGo = new GameObject("Terrain");
                Undo.RegisterCreatedObjectUndo(terrainGo, "Generate Terrain");
                terrainGo.transform.SetParent(builder.transform, false);
                builder.TerrainObject = terrainGo;
                EditorUtility.SetDirty(builder);
            }
            else
            {
                Undo.RecordObject(terrainGo, "Generate Terrain");
            }

            MeshFilter filter = GetOrAdd<MeshFilter>(terrainGo);
            MeshRenderer renderer = GetOrAdd<MeshRenderer>(terrainGo);
            MeshCollider collider = GetOrAdd<MeshCollider>(terrainGo);

            filter.sharedMesh = asset;
            collider.sharedMesh = null;
            collider.sharedMesh = asset;

            if (builder.TerrainMaterial != null)
            {
                renderer.sharedMaterial = builder.TerrainMaterial;
            }
            else if (renderer.sharedMaterial == null)
            {
                Debug.LogWarning("No Terrain Material assigned.", builder);
            }

            GameObjectUtility.SetStaticEditorFlags(terrainGo,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic);

            MarkSceneDirty(builder);
            Debug.Log($"Terrain generated: {asset.vertexCount} verts, saved to {meshPath}");
        }

        private static void ScatterProps(WorldBuilder builder)
        {
            if (builder.TerrainObject == null)
            {
                EditorUtility.DisplayDialog("Agrestis", "Generate the terrain first.", "OK");
                return;
            }

            Transform parent = EnsurePropsParent(builder);
            System.Random rng = new System.Random(builder.Seed ^ 0x5EED);
            TerrainGenerator sampler = builder.Sampler;
            int placed = 0;

            try
            {
                for (int layerIndex = 0; layerIndex < builder.Layers.Count; layerIndex++)
                {
                    WorldBuilder.ScatterLayer layer = builder.Layers[layerIndex];
                    if (!layer.Enabled || layer.Prefabs == null || layer.Prefabs.Length == 0) continue;

                    Transform layerParent = EnsureChild(parent, layer.Name);

                    for (int i = 0; i < layer.Count; i++)
                    {
                        if ((i & 31) == 0)
                        {
                            EditorUtility.DisplayProgressBar("Agrestis",
                                $"Scattering {layer.Name} ({i}/{layer.Count})",
                                (layerIndex + i / (float)Mathf.Max(1, layer.Count)) / builder.Layers.Count);
                        }

                        if (!TryFindSpot(sampler, rng, layer, out Vector3 position, out Vector3 normal)) continue;

                        GameObject prefab = layer.Prefabs[rng.Next(layer.Prefabs.Length)];
                        if (prefab == null) continue;

                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layerParent);
                        Undo.RegisterCreatedObjectUndo(instance, "Scatter Props");

                        instance.transform.position = position - Vector3.up * layer.SinkIntoGround;

                        Quaternion rotation = layer.AlignToGround
                            ? Quaternion.FromToRotation(Vector3.up, normal)
                            : Quaternion.identity;

                        if (layer.RandomYaw)
                            rotation *= Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                        if (layer.RandomTilt > 0f)
                        {
                            rotation *= Quaternion.Euler(
                                ((float)rng.NextDouble() * 2f - 1f) * layer.RandomTilt, 0f,
                                ((float)rng.NextDouble() * 2f - 1f) * layer.RandomTilt);
                        }

                        instance.transform.rotation = rotation;

                        float scale = Mathf.Lerp(layer.ScaleRange.x, layer.ScaleRange.y, (float)rng.NextDouble());
                        instance.transform.localScale = Vector3.one * scale;

                        if (layer.Sways && builder.Wind != null)
                        {
                            SwayTag tag = Undo.AddComponent<SwayTag>(instance);
                            tag.Responsiveness = layer.SwayResponsiveness;
                        }
                        else
                        {
                            GameObjectUtility.SetStaticEditorFlags(instance,
                                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
                        }

                        placed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            MarkSceneDirty(builder);
            Debug.Log($"Scattered {placed} props.");
        }

        private static bool TryFindSpot(TerrainGenerator sampler, System.Random rng,
            WorldBuilder.ScatterLayer layer, out Vector3 position, out Vector3 normal)
        {
            float usable = sampler.Config.Size * 0.5f - layer.EdgeMargin;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;

                float height = sampler.SampleHeight(x, z);
                float altitude = height - sampler.Config.WaterLevel;

                if (altitude < layer.MinAltitude) continue;
                if (layer.MaxAltitude > 0f && altitude > layer.MaxAltitude) continue;
                if (sampler.SampleSlope(x, z) > layer.MaxSlope) continue;

                position = new Vector3(x, height, z);
                normal = sampler.SampleNormal(x, z);
                return true;
            }

            position = Vector3.zero;
            normal = Vector3.up;
            return false;
        }

        private static void ClearProps(WorldBuilder builder)
        {
            if (builder.PropsParent == null) return;
            if (!EditorUtility.DisplayDialog("Agrestis",
                    "Delete all scattered props?",
                    "Delete", "Cancel")) return;

            Undo.DestroyObjectImmediate(builder.PropsParent.gameObject);
            builder.PropsParent = null;
            EditorUtility.SetDirty(builder);
            MarkSceneDirty(builder);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static Transform EnsurePropsParent(WorldBuilder builder)
        {
            if (builder.PropsParent != null) return builder.PropsParent;

            GameObject go = new GameObject("Props");
            Undo.RegisterCreatedObjectUndo(go, "Create Props Parent");
            go.transform.SetParent(builder.transform, false);
            builder.PropsParent = go.transform;
            EditorUtility.SetDirty(builder);
            return go.transform;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Layer Parent");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void MarkSceneDirty(WorldBuilder builder)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }
    }
}
