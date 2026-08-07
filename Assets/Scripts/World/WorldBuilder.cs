using System.Collections.Generic;
using UnityEngine;
using Agrestis.Core;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class WorldBuilder : MonoBehaviour
    {
        [System.Serializable]
        public class ScatterLayer
        {
            public string Name = "Trees";
            public bool Enabled = true;

            [Tooltip("Prefabs picked at random.")]
            public GameObject[] Prefabs;

            [Tooltip("Number of placement attempts.")]
            public int Count = 200;

            [Header("Placement rules")]
            [Tooltip("Minimum height above water.")]
            public float MinAltitude = 1.5f;
            [Tooltip("Maximum height above water. 0 = no limit.")]
            public float MaxAltitude;
            [Tooltip("Steepest ground in degrees.")]
            public float MaxSlope = 28f;
            [Tooltip("Distance to keep from the map edge.")]
            public float EdgeMargin = 12f;

            [Header("Variation")]
            public Vector2 ScaleRange = new Vector2(0.85f, 1.4f);
            public bool RandomYaw = true;
            [Tooltip("Rotate to match the ground normal.")]
            public bool AlignToGround;
            [Tooltip("Extra random tilt in degrees.")]
            public float RandomTilt;
            [Tooltip("Sink into the ground by this much.")]
            public float SinkIntoGround;

            [Header("Wind")]
            [Tooltip("Add a SwayTag so it moves in the wind.")]
            public bool Sways;
            [Range(0f, 3f)] public float SwayResponsiveness = 1f;
        }

        [Header("Seed")]
        [Tooltip("Seed for terrain and scattering.")]
        public int Seed = 20260807;

        [Header("Terrain")]
        public TerrainGenerator.Settings Terrain = TerrainGenerator.Settings.Default;
        [Tooltip("Material for the terrain mesh.")]
        public Material TerrainMaterial;
        [Tooltip("Folder the terrain mesh is saved to.")]
        public string MeshSaveFolder = "Assets/Meshes";

        [Header("Scatter layers")]
        public List<ScatterLayer> Layers = new List<ScatterLayer>();

        [Header("Scene containers (created automatically)")]
        [Tooltip("Generated terrain object.")]
        public GameObject TerrainObject;
        [Tooltip("Parent for scattered props.")]
        public Transform PropsParent;
        [Tooltip("Wind Animator that swaying props register with.")]
        public WindAnimator Wind;

        private TerrainGenerator _sampler;
        private int _samplerSeed = int.MinValue;

        public TerrainGenerator Sampler
        {
            get
            {
                if (_sampler == null || _samplerSeed != Seed)
                {
                    _sampler = new TerrainGenerator(Terrain, Seed, Palette.FromSeed(Seed));
                    _samplerSeed = Seed;
                }
                return _sampler;
            }
        }

        public void InvalidateSampler() => _sampler = null;

        public float SampleHeight(float x, float z) => Sampler.SampleHeight(x, z);
        public float SampleSlope(float x, float z) => Sampler.SampleSlope(x, z);
        public Vector3 SampleNormal(float x, float z) => Sampler.SampleNormal(x, z);

        public Vector3 SnapToGround(Vector3 position)
        {
            position.y = SampleHeight(position.x, position.z);
            return position;
        }

        private void Reset()
        {
            Terrain = TerrainGenerator.Settings.Default;
            Layers = new List<ScatterLayer>
            {
                new ScatterLayer
                {
                    Name = "Broadleaf trees", Count = 240,
                    MinAltitude = 2f, MaxAltitude = 26f, MaxSlope = 26f,
                    ScaleRange = new Vector2(0.85f, 1.4f), Sways = true, SwayResponsiveness = 1f
                },
                new ScatterLayer
                {
                    Name = "Pines", Count = 140,
                    MinAltitude = 14f, MaxAltitude = 0f, MaxSlope = 34f,
                    ScaleRange = new Vector2(0.8f, 1.5f), Sways = true, SwayResponsiveness = 0.55f
                },
                new ScatterLayer
                {
                    Name = "Boulders", Count = 110,
                    MinAltitude = 0.5f, MaxAltitude = 0f, MaxSlope = 40f,
                    ScaleRange = new Vector2(0.7f, 2.6f), AlignToGround = true, RandomTilt = 20f,
                    SinkIntoGround = 0.35f
                },
                new ScatterLayer
                {
                    Name = "Bushes", Count = 180,
                    MinAltitude = 0.3f, MaxAltitude = 22f, MaxSlope = 30f,
                    ScaleRange = new Vector2(0.7f, 1.5f), Sways = true, SwayResponsiveness = 1.6f
                }
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float half = Terrain.Size * 0.5f;

            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(
                new Vector3(0f, Terrain.WaterLevel + Terrain.MountainAmplitude * 0.5f, 0f),
                new Vector3(Terrain.Size, Terrain.MountainAmplitude, Terrain.Size));

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(0f, Terrain.WaterLevel, 0f),
                                new Vector3(Terrain.Size, 0.02f, Terrain.Size));

            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.35f);
            Gizmos.DrawWireCube(new Vector3(0f, Terrain.WaterLevel, 0f),
                                new Vector3(half * 1.36f, 0.02f, half * 1.36f));
        }
#endif
    }
}
