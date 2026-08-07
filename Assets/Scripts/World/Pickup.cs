using UnityEngine;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Heart, Stamina }

        [Header("Effect")]
        public Kind Type = Kind.Heart;
        [Tooltip("Quarter hearts, or stamina wheels.")]
        public float Amount = 4f;

        [Header("Lifetime")]
        [Tooltip("Seconds before it disappears. 0 = never.")]
        public float Lifetime;

        [Header("Motion")]
        public float BobHeight = 0.18f;
        public float BobSpeed = 2.2f;
        public float SpinSpeed = 90f;
        [Tooltip("Transform that bobs and spins.")]
        [SerializeField] private Transform _visual;

        [Header("Feedback")]
        public string HeartMessage = "Hearty fruit";
        public string StaminaMessage = "Stamina restored";

        private Vector3 _restPosition;
        private float _phase;

        private void Reset()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;
            _visual = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        private void Start()
        {
            if (_visual == null && transform.childCount > 0) _visual = transform.GetChild(0);
            _restPosition = _visual != null ? _visual.localPosition : Vector3.zero;
            _phase = Random.value * Mathf.PI * 2f;

            if (Lifetime > 0f) Destroy(gameObject, Lifetime);
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * BobSpeed + _phase) * BobHeight;

            if (_visual != null)
            {
                _visual.localPosition = _restPosition + Vector3.up * bob;
                _visual.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.Self);
            }
            else
            {
                transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            if (Type == Kind.Heart)
            {
                if (player.Health == null || player.Health.CurrentQuarters >= player.Health.MaxQuarters) return;
                player.Health.Heal(Mathf.RoundToInt(Amount));
                GameEvents.RaiseAnnouncement($"{HeartMessage}  +{Amount / 4f:0.##} hearts", 1.6f);
            }
            else
            {
                if (player.Stamina == null) return;
                player.Stamina.Refill();
                GameEvents.RaiseAnnouncement(StaminaMessage, 1.6f);
            }

            Destroy(gameObject);
        }

        public static GameObject HeartDropPrefab;
        public static GameObject StaminaDropPrefab;

        public static Pickup SpawnHeartDrop(Vector3 position)
        {
            if (HeartDropPrefab == null) return null;
            GameObject go = Instantiate(HeartDropPrefab, position, Quaternion.identity);
            Pickup pickup = go.GetComponent<Pickup>();
            if (pickup != null && pickup.Lifetime <= 0f) pickup.Lifetime = 45f;
            return pickup;
        }

        public static Pickup SpawnStaminaDrop(Vector3 position)
        {
            if (StaminaDropPrefab == null) return null;
            GameObject go = Instantiate(StaminaDropPrefab, position, Quaternion.identity);
            Pickup pickup = go.GetComponent<Pickup>();
            if (pickup != null && pickup.Lifetime <= 0f) pickup.Lifetime = 45f;
            return pickup;
        }
    }
}
