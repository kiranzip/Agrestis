using UnityEngine;
using Agrestis.Player;

namespace Agrestis.World
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class WaterVolume : MonoBehaviour
    {
        public enum SurfaceMode
        {
            TopOfCollider,

            ExplicitHeight,

            FollowTransform
        }

        [Header("Waterline")]
        public SurfaceMode Mode = SurfaceMode.TopOfCollider;
        public float ExplicitSurfaceY;
        public Transform SurfaceTransform;

        private BoxCollider _box;

        public float SurfaceY
        {
            get
            {
                switch (Mode)
                {
                    case SurfaceMode.ExplicitHeight:
                        return ExplicitSurfaceY;
                    case SurfaceMode.FollowTransform:
                        return SurfaceTransform != null ? SurfaceTransform.position.y : transform.position.y;
                    default:
                        if (_box == null) _box = GetComponent<BoxCollider>();
                        return _box.bounds.max.y;
                }
            }
        }

        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(100f, 40f, 100f);
            box.center = new Vector3(0f, -20f, 0f);
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (!_box.isTrigger)
            {
                Debug.LogWarning($"WaterVolume {name} was not a trigger.", this);
                _box.isTrigger = true;
            }

            if (SurfaceTransform == null)
            {
                Transform found = transform.Find("Surface");
                if (found != null) SurfaceTransform = found;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null) player.SetWater(this);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null) player.ClearWater(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
            Vector3 centre = box.bounds.center;
            Gizmos.DrawWireCube(new Vector3(centre.x, SurfaceY, centre.z), new Vector3(size.x, 0.02f, size.z));
        }
#endif
    }
}
