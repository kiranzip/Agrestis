using UnityEngine;

namespace Agrestis.Core
{
    public static class Noise
    {
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFF) / (float)0x8000000;
            }
        }

        public static float Value(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            float u = xf * xf * xf * (xf * (xf * 6f - 15f) + 10f);
            float v = yf * yf * yf * (yf * (yf * 6f - 15f) + 10f);

            float a = Hash01(xi, yi, seed);
            float b = Hash01(xi + 1, yi, seed);
            float c = Hash01(xi, yi + 1, seed);
            float d = Hash01(xi + 1, yi + 1, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float FBM(float x, float y, int seed, int octaves = 5, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float totalAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x * frequency, y * frequency, seed + i * 977) * amplitude;
                totalAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return totalAmplitude > 0f ? sum / totalAmplitude : 0f;
        }

        public static float Ridged(float x, float y, int seed, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float totalAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Value(x * frequency, y * frequency, seed + i * 6151) * 2f - 1f);
                sum += n * n * amplitude;
                totalAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return totalAmplitude > 0f ? sum / totalAmplitude : 0f;
        }
    }
}
