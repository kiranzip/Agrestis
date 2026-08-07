using UnityEngine;
using Agrestis.Core;

namespace Agrestis.World
{
    public class TerrainGenerator
    {
        [System.Serializable]
        public struct Settings
        {
            public float Size;
            public int Resolution;
            public float BaseFrequency;
            public float HillAmplitude;
            public float MountainAmplitude;
            public float WaterLevel;
            public float ShoreBand;

            public static Settings Default => new Settings
            {
                Size = 300f,
                Resolution = 200,
                BaseFrequency = 0.012f,
                HillAmplitude = 9f,
                MountainAmplitude = 46f,
                WaterLevel = 0f,
                ShoreBand = 1.6f
            };
        }

        public Settings Config { get; private set; }
        public int Seed { get; private set; }
        public Palette Colours { get; private set; }

        private readonly float _halfSize;

        public TerrainGenerator(Settings settings, int seed, Palette palette)
        {
            Config = settings;
            Seed = seed;
            Colours = palette;
            _halfSize = settings.Size * 0.5f;
        }

        public float SampleHeight(float x, float z)
        {
            float f = Config.BaseFrequency;
            float nx = x * f;
            float nz = z * f;

            float hills = Noise.FBM(nx, nz, Seed, 5) * Config.HillAmplitude;

            float mask = SmoothStep01(0.42f, 0.78f, Noise.FBM(nx * 0.32f, nz * 0.32f, Seed + 4409, 3));
            float ridge = Noise.Ridged(nx * 0.75f, nz * 0.75f, Seed + 991, 4);

            float mountains = Mathf.Pow(ridge, 2.2f) * mask * Config.MountainAmplitude;

            float detail = (Noise.FBM(nx * 6.5f, nz * 6.5f, Seed + 7717, 2) - 0.5f) * 1.1f;

            float height = hills + mountains + detail;

            float distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / _halfSize;
            float falloff = 1f - SmoothStep01(0.68f, 0.99f, distance);
            height = height * falloff - (1f - falloff) * 14f;

            return height + Config.WaterLevel;
        }

        public Vector3 SampleNormal(float x, float z, float epsilon = 0.6f)
        {
            float hL = SampleHeight(x - epsilon, z);
            float hR = SampleHeight(x + epsilon, z);
            float hD = SampleHeight(x, z - epsilon);
            float hU = SampleHeight(x, z + epsilon);
            return new Vector3(hL - hR, 2f * epsilon, hD - hU).normalized;
        }

        public float SampleSlope(float x, float z) => Vector3.Angle(SampleNormal(x, z), Vector3.up);

        public bool IsUnderwater(float x, float z) => SampleHeight(x, z) < Config.WaterLevel;

        private static float SmoothStep01(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private Color BiomeColour(float x, float z, float height, Vector3 normal)
        {
            float slope = Vector3.Angle(normal, Vector3.up);
            float aboveWater = height - Config.WaterLevel;

            if (slope > 42f)
            {
                float t = Mathf.InverseLerp(42f, 70f, slope);
                Color rock = Color.Lerp(Colours.GrassDark, Colours.Rock, t);
                if (aboveWater > 30f) rock = Color.Lerp(rock, Colours.Snow, Mathf.InverseLerp(30f, 44f, aboveWater) * 0.6f);
                return Vary(rock, x, z, 0.09f);
            }

            if (aboveWater < Config.ShoreBand)
                return Vary(Colours.Sand, x, z, 0.07f);

            if (aboveWater > 34f)
                return Vary(Color.Lerp(Colours.Rock, Colours.Snow, Mathf.InverseLerp(34f, 46f, aboveWater)), x, z, 0.05f);

            float lushness = Noise.FBM(x * 0.03f, z * 0.03f, Seed + 313, 3);
            Color grass = Color.Lerp(Colours.GrassDark, Colours.Grass, lushness);
            return Vary(grass, x, z, 0.08f);
        }

        private Color Vary(Color baseColour, float x, float z, float amount)
        {
            float n = Noise.Value(x * 0.9f, z * 0.9f, Seed + 5501) - 0.5f;
            float m = 1f + n * amount * 2f;
            return new Color(baseColour.r * m, baseColour.g * m, baseColour.b * m, 1f);
        }

        public Mesh BuildMesh()
        {
            int res = Mathf.Max(8, Config.Resolution);
            int stride = res + 1;
            float step = Config.Size / res;

            Vector3[] vertices = new Vector3[stride * stride];
            Color[] colours = new Color[stride * stride];
            Vector2[] uvs = new Vector2[stride * stride];
            int[] triangles = new int[res * res * 6];

            for (int y = 0; y <= res; y++)
            {
                for (int x = 0; x <= res; x++)
                {
                    int i = y * stride + x;
                    float wx = -_halfSize + x * step;
                    float wz = -_halfSize + y * step;
                    float h = SampleHeight(wx, wz);

                    vertices[i] = new Vector3(wx, h, wz);
                    uvs[i] = new Vector2(x / (float)res, y / (float)res);
                    colours[i] = BiomeColour(wx, wz, h, SampleNormal(wx, wz, step));
                }
            }

            int t = 0;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = y * stride + x;
                    triangles[t++] = i;
                    triangles[t++] = i + stride;
                    triangles[t++] = i + stride + 1;
                    triangles[t++] = i;
                    triangles[t++] = i + stride + 1;
                    triangles[t++] = i + 1;
                }
            }

            Mesh mesh = new Mesh { name = "ProcTerrain_" + Seed };

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colours;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public bool TryFindPoint(System.Random rng, float minHeightAboveWater, float maxSlope,
            out Vector3 point, float margin = 12f, int attempts = 40)
        {
            float usable = _halfSize - margin;
            for (int i = 0; i < attempts; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;
                float h = SampleHeight(x, z);

                if (h - Config.WaterLevel < minHeightAboveWater) continue;
                if (SampleSlope(x, z) > maxSlope) continue;

                point = new Vector3(x, h, z);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        public bool TryFindPeak(System.Random rng, out Vector3 point, float minAltitude = 16f, int attempts = 220)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            float usable = _halfSize - 24f;

            for (int i = 0; i < attempts; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * usable;
                float h = SampleHeight(x, z);
                float slope = SampleSlope(x, z);

                if (slope > 22f) continue;
                if (h - Config.WaterLevel < minAltitude) continue;

                float surroundingSteepness = 0f;
                for (int s = 0; s < 4; s++)
                {
                    float a = s * Mathf.PI * 0.5f;
                    surroundingSteepness += SampleSlope(x + Mathf.Cos(a) * 9f, z + Mathf.Sin(a) * 9f);
                }

                float score = h * 1.5f + surroundingSteepness * 0.4f - slope;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Vector3(x, h, z);
                }
            }

            point = best;
            return bestScore > float.MinValue;
        }
    }
}
