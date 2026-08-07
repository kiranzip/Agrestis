using UnityEngine;

namespace Agrestis.Core
{
    [System.Serializable]
    public struct Palette
    {
        public Color DeepWater;
        public Color Sand;
        public Color Grass;
        public Color GrassDark;
        public Color Rock;
        public Color Snow;
        public Color TrunkColour;
        public Color LeafColour;
        public Color Sky;
        public Color Fog;

        public static Palette FromSeed(int seed)
        {
            System.Random rng = new System.Random(seed);
            float hueShift = (float)rng.NextDouble();

            Color Shift(Color baseColour, float amount)
            {
                Color.RGBToHSV(baseColour, out float h, out float s, out float v);
                h = Mathf.Repeat(h + hueShift * amount, 1f);
                return Color.HSVToRGB(h, s, v);
            }

            return new Palette
            {
                DeepWater   = Shift(new Color(0.06f, 0.20f, 0.34f), 0.05f),
                Sand        = Shift(new Color(0.83f, 0.75f, 0.53f), 0.06f),
                Grass       = Shift(new Color(0.33f, 0.54f, 0.25f), 0.10f),
                GrassDark   = Shift(new Color(0.21f, 0.38f, 0.18f), 0.10f),
                Rock        = Shift(new Color(0.45f, 0.44f, 0.42f), 0.04f),
                Snow        = new Color(0.93f, 0.95f, 0.97f),
                TrunkColour = Shift(new Color(0.32f, 0.23f, 0.16f), 0.05f),
                LeafColour  = Shift(new Color(0.28f, 0.50f, 0.22f), 0.14f),
                Sky         = Shift(new Color(0.45f, 0.66f, 0.88f), 0.04f),
                Fog         = Shift(new Color(0.66f, 0.76f, 0.85f), 0.04f)
            };
        }
    }
}
