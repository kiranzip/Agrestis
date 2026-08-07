using UnityEngine;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class GateOfPassage : MonoBehaviour, IInteractable
    {
        [Header("Scene references")]
        [Tooltip("Portal plane between the posts.")]
        [SerializeField] private Transform _portal;
        [SerializeField] private Renderer _portalRenderer;
        [SerializeField] private Light _glow;

        [Header("Presentation")]
        public Color SealedColour = new Color(0.28f, 0.30f, 0.36f);
        public Color OpenColour = new Color(0.55f, 0.90f, 1.00f);
        public float SealedPulseSpeed = 0.8f;
        public float OpenPulseSpeed = 2.6f;
        public float SealedLightIntensity = 0.6f;
        public float OpenLightIntensity = 4f;

        [Header("Prompts")]
        public string OpenPrompt = "Enter the Gate of Passage  [E]";
        public string SealedPrompt = "The gate is sealed - claim every Spirit Orb";

        [Header("Behaviour")]
        [Tooltip("Open by walking through as well as pressing E.")]
        public bool TriggerOnWalkThrough = true;

        public bool IsOpen { get; private set; }

        public string Prompt => IsOpen ? OpenPrompt : SealedPrompt;
        public bool IsAvailable => true;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.5f;

        private Material _portalMaterial;
        private Vector3 _portalRestScale = Vector3.one;
        private bool _used;

        private void Reset()
        {
            _portal = transform.Find("Portal");
            if (_portal != null) _portalRenderer = _portal.GetComponent<Renderer>();
            _glow = GetComponentInChildren<Light>();

            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 5f, 1.5f);
            trigger.center = new Vector3(0f, 2.5f, 0f);
        }

        private void Awake()
        {
            if (_portal != null) _portalRestScale = _portal.localScale;
            if (_portalRenderer != null) _portalMaterial = _portalRenderer.material;
            ApplyVisualState();
        }

        private void Update()
        {
            if (_portal == null) return;

            float speed = IsOpen ? OpenPulseSpeed : SealedPulseSpeed;
            float amplitude = IsOpen ? 0.09f : 0.03f;
            float pulse = 1f + Mathf.Sin(Time.time * speed) * amplitude;
            _portal.localScale = new Vector3(_portalRestScale.x * pulse, _portalRestScale.y, _portalRestScale.z * pulse);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            ApplyVisualState();
        }

        public void Interact(GameObject interactor)
        {
            if (!IsOpen)
            {
                GameEvents.RaiseAnnouncement("The gate is sealed. Claim every Spirit Orb first.", 2.5f);
                return;
            }

            if (interactor.GetComponentInParent<PlayerController>() == null) return;
            Pass();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TriggerOnWalkThrough || !IsOpen) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            Pass();
        }

        private void Pass()
        {
            if (_used) return;
            _used = true;
            GameManager.Instance?.AdvanceLevel();
        }

        private void ApplyVisualState()
        {
            Color colour = IsOpen ? OpenColour : SealedColour;

            if (_portalMaterial != null)
            {
                if (_portalMaterial.HasProperty("_BaseColor")) _portalMaterial.SetColor("_BaseColor", colour);
                if (_portalMaterial.HasProperty("_Color")) _portalMaterial.SetColor("_Color", colour);
                if (_portalMaterial.HasProperty("_EmissionColor"))
                {
                    _portalMaterial.EnableKeyword("_EMISSION");
                    _portalMaterial.SetColor("_EmissionColor", colour * (IsOpen ? 4f : 1f));
                }
            }

            if (_glow != null)
            {
                _glow.color = colour;
                _glow.intensity = IsOpen ? OpenLightIntensity : SealedLightIntensity;
            }
        }
    }
}
