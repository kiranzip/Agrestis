using System.Collections.Generic;
using UnityEngine;

namespace Agrestis.World
{
    public class WindAnimator : MonoBehaviour
    {
        [Header("Wind")]
        public Vector3 WindDirection = new Vector3(1f, 0f, 0.4f);
        [Tooltip("Peak lean in degrees.")]
        public float Strength = 3.2f;
        public float SwellSpeed = 0.55f;
        public float FlutterSpeed = 2.4f;
        [Tooltip("Distance between gusts.")]
        public float GustWavelength = 26f;

        [Header("Collection")]
        [Tooltip("Find SwayTag objects at startup.")]
        public bool CollectTaggedObjectsOnStart = true;

        private readonly List<Transform> _foliage = new List<Transform>();
        private readonly List<Quaternion> _restRotations = new List<Quaternion>();
        private readonly List<float> _scales = new List<float>();

        private void Start()
        {
            if (!CollectTaggedObjectsOnStart) return;

            foreach (SwayTag tag in FindObjectsByType<SwayTag>(FindObjectsSortMode.None))
                Register(tag.transform, tag.Responsiveness);
        }

        public void Register(Transform t, float responsiveness = 1f)
        {
            if (t == null) return;
            _foliage.Add(t);
            _restRotations.Add(t.localRotation);
            _scales.Add(responsiveness);
        }

        public void Clear()
        {
            _foliage.Clear();
            _restRotations.Clear();
            _scales.Clear();
        }

        private void Update()
        {
            if (_foliage.Count == 0) return;

            Vector3 wind = WindDirection.sqrMagnitude > 0.0001f ? WindDirection.normalized : Vector3.right;

            Vector3 axis = Vector3.Cross(Vector3.up, wind).normalized;
            float time = Time.time;

            for (int i = _foliage.Count - 1; i >= 0; i--)
            {
                Transform t = _foliage[i];
                if (t == null)
                {
                    _foliage.RemoveAt(i);
                    _restRotations.RemoveAt(i);
                    _scales.RemoveAt(i);
                    continue;
                }

                Vector3 p = t.position;
                float phase = (p.x + p.z) / GustWavelength;
                float swell = Mathf.Sin(time * SwellSpeed + phase);
                float flutter = Mathf.Sin(time * FlutterSpeed + phase * 2.7f) * 0.35f;
                float angle = (swell + flutter) * Strength * _scales[i];

                t.localRotation = Quaternion.AngleAxis(angle, axis) * _restRotations[i];
            }
        }
    }
}
