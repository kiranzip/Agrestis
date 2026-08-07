using UnityEngine;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class Shrine : MonoBehaviour, IInteractable
    {
        [Header("Scene references")]
        [Tooltip("Floating orb transform.")]
        [SerializeField] private Transform _orb;
        [Tooltip("Renderer whose colour changes when activated.")]
        [SerializeField] private Renderer _orbRenderer;
        [Tooltip("Point light at the orb.")]
        [SerializeField] private Light _glow;
        [Tooltip("Respawn point for this shrine.")]
        [SerializeField] private Transform _respawnPoint;

        [Header("State")]
        [Tooltip("Start already activated.")]
        public bool Activated;

        [Header("Presentation")]
        public Color DormantColour = new Color(0.35f, 0.45f, 0.55f);
        public Color ActiveColour = new Color(0.35f, 0.95f, 1f);
        public string PromptText = "Touch the shrine  [E]";
        [Tooltip("Orb bob height in metres.")]
        public float BobHeight = 0.14f;
        public float DormantBobSpeed = 1.1f;
        public float ActiveBobSpeed = 2.4f;
        public float DormantSpin = 24f;
        public float ActiveSpin = 90f;
        public float DormantLightIntensity = 1.6f;
        public float ActiveLightIntensity = 3.4f;

        [Header("Reward")]
        [Tooltip("Restore health and stamina on activation.")]
        public bool RestorePlayer = true;

        public string Prompt => Activated ? null : PromptText;
        public bool IsAvailable => !Activated;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.2f;

        public Vector3 RespawnPosition => _respawnPoint != null
            ? _respawnPoint.position
            : transform.position + transform.forward * 2f + Vector3.up * 0.5f;

        private Material _orbMaterial;
        private Vector3 _orbRestPosition;
        private float _phase;

        private void Reset()
        {
            _orb = transform.Find("Orb");
            if (_orb != null)
            {
                _orbRenderer = _orb.GetComponent<Renderer>();
                _glow = _orb.GetComponentInChildren<Light>();
            }
            _respawnPoint = transform.Find("RespawnPoint");

            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 3.2f;
            trigger.center = Vector3.up;
        }

        private void Awake()
        {
            if (_orb != null) _orbRestPosition = _orb.localPosition;

            if (_orbRenderer != null) _orbMaterial = _orbRenderer.material;

            ApplyVisualState();
        }

        private void Update()
        {
            if (_orb == null) return;

            _phase += Time.deltaTime;

            float bobSpeed = Activated ? ActiveBobSpeed : DormantBobSpeed;
            _orb.localPosition = _orbRestPosition + Vector3.up * (Mathf.Sin(_phase * bobSpeed) * BobHeight);
            _orb.Rotate(Vector3.up, (Activated ? ActiveSpin : DormantSpin) * Time.deltaTime, Space.Self);

            if (_glow != null)
            {
                float baseIntensity = Activated ? ActiveLightIntensity : DormantLightIntensity;
                _glow.intensity = baseIntensity + Mathf.Sin(_phase * bobSpeed * 1.7f) * 0.5f;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Activated) return;

            PlayerController player = interactor.GetComponentInParent<PlayerController>();
            if (player == null) return;

            Activated = true;
            ApplyVisualState();

            if (RestorePlayer)
            {
                player.Health?.FullHeal();
                player.Stamina?.Refill();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetRespawnPoint(RespawnPosition);
                GameManager.Instance.CollectOrb();
            }

            GameEvents.RaiseInteractPrompt(null);
        }

        private void ApplyVisualState()
        {
            Color colour = Activated ? ActiveColour : DormantColour;

            if (_orbMaterial != null)
            {
                if (_orbMaterial.HasProperty("_BaseColor")) _orbMaterial.SetColor("_BaseColor", colour);
                if (_orbMaterial.HasProperty("_Color")) _orbMaterial.SetColor("_Color", colour);
                if (_orbMaterial.HasProperty("_EmissionColor"))
                {
                    _orbMaterial.EnableKeyword("_EMISSION");
                    _orbMaterial.SetColor("_EmissionColor", colour * (Activated ? 3f : 1f));
                }
            }

            if (_glow != null) _glow.color = colour;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Activated ? ActiveColour : DormantColour;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 3.2f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(RespawnPosition, new Vector3(0.5f, 1.8f, 0.5f));
        }
#endif
    }
}
