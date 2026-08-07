using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.UI
{
    [DisallowMultipleComponent]
    public class HUDController : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Canvas Scaler on the HUD canvas.")]
        [SerializeField] private CanvasScaler _scaler;
        [Tooltip("Root rect inset into the screen safe area.")]
        [SerializeField] private RectTransform _safeArea;

        [Header("Hearts")]
        [Tooltip("Parent the heart icons are spawned under.")]
        [SerializeField] private RectTransform _heartsContainer;
        [Tooltip("Heart icon prefab.")]
        [SerializeField] private HeartIcon _heartPrefab;
        [SerializeField] private float _heartSpacing = 42f;
        [SerializeField] private int _heartsPerRow = 10;

        [Header("Stamina")]
        [Tooltip("Parent of the stamina rings.")]
        [SerializeField] private RectTransform _staminaContainer;
        [Tooltip("Ring fill images, outermost first.")]
        [SerializeField] private Image[] _staminaRings;
        [Tooltip("Backing rings, same order.")]
        [SerializeField] private Image[] _staminaRingBackings;
        [SerializeField] private Color _staminaColour = new Color(0.45f, 0.92f, 0.42f);
        [SerializeField] private Color _exhaustedColour = new Color(1f, 0.55f, 0.2f);
        [Tooltip("Hide the wheel when it is full.")]
        [SerializeField] private bool _hideWheelWhenFull = true;

        [Header("Text")]
        [SerializeField] private Text _objectiveText;
        [SerializeField] private Text _promptText;
        [SerializeField] private Text _announcementText;
        [SerializeField] private Text _statsText;

        [Header("Overlays")]
        [Tooltip("Full screen image used for the damage flash.")]
        [SerializeField] private Image _damageFlash;
        [Tooltip("Panel shown while paused.")]
        [SerializeField] private GameObject _pausePanel;

        [Header("Layout")]
        public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Wheel position in landscape.")]
        public Vector2 WheelLandscapePosition = new Vector2(130f, -70f);
        [Tooltip("Wheel position in portrait.")]
        public Vector2 WheelPortraitPosition = new Vector2(-40f, -190f);

        [Header("Statistics")]
        public bool ShowStats = true;
        public float StatsSampleWindow = 0.5f;
        [Tooltip("Hide the stats panel below this width.")]
        public float StatsMinScreenWidth = 700f;

        private readonly List<HeartIcon> _hearts = new List<HeartIcon>();
        private int _currentQuarters = 12;
        private int _maxQuarters = 12;
        private float _stamina = 1f;
        private float _staminaMax = 1f;
        private bool _exhausted;
        private float _announcementUntil;
        private float _flashUntil;
        private CanvasGroup _wheelGroup;
        private Rect _lastSafeArea;
        private Vector2 _lastScreen;

        private float _sampleTimer, _sampleTime, _worstFrame;
        private int _sampleFrames;
        private float _displayFps = 60f, _displayWorstMs;
        private void Reset()
        {
            _scaler = GetComponentInParent<CanvasScaler>();
            Transform safe = transform.Find("SafeArea");
            if (safe != null) _safeArea = safe as RectTransform;
        }

        private void Awake()
        {
            if (_scaler == null) _scaler = GetComponentInParent<CanvasScaler>();
            if (_safeArea == null) _safeArea = transform as RectTransform;

            if (_staminaContainer != null)
            {
                _wheelGroup = _staminaContainer.GetComponent<CanvasGroup>();
                if (_wheelGroup == null) _wheelGroup = _staminaContainer.gameObject.AddComponent<CanvasGroup>();
            }

            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_promptText != null) _promptText.text = "";
            if (_announcementText != null) _announcementText.text = "";

            ApplyResponsiveLayout(force: true);
            RebuildHearts();
        }

        private void OnEnable()
        {
            GameEvents.HealthChanged += OnHealthChanged;
            GameEvents.StaminaChanged += OnStaminaChanged;
            GameEvents.Announcement += OnAnnouncement;
            GameEvents.InteractPromptChanged += OnPrompt;
            GameEvents.LevelGenerated += OnRegionEntered;
            GameEvents.PlayerDied += OnPlayerDied;
        }

        private void OnDisable()
        {
            GameEvents.HealthChanged -= OnHealthChanged;
            GameEvents.StaminaChanged -= OnStaminaChanged;
            GameEvents.Announcement -= OnAnnouncement;
            GameEvents.InteractPromptChanged -= OnPrompt;
            GameEvents.LevelGenerated -= OnRegionEntered;
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        private void RebuildHearts()
        {
            if (_heartsContainer == null || _heartPrefab == null) return;

            int wanted = Mathf.Max(1, _maxQuarters / HealthSystem.QuartersPerHeart);

            while (_hearts.Count < wanted)
            {
                HeartIcon icon = Instantiate(_heartPrefab, _heartsContainer);
                icon.name = $"Heart{_hearts.Count:00}";
                _hearts.Add(icon);
            }
            while (_hearts.Count > wanted)
            {
                HeartIcon last = _hearts[_hearts.Count - 1];
                _hearts.RemoveAt(_hearts.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }

            for (int i = 0; i < _hearts.Count; i++)
            {
                RectTransform rt = (RectTransform)_hearts[i].transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(
                    (i % _heartsPerRow) * _heartSpacing,
                    -(i / _heartsPerRow) * _heartSpacing);
            }

            RefreshHearts();
        }

        private void RefreshHearts()
        {
            for (int i = 0; i < _hearts.Count; i++)
            {
                int quarters = Mathf.Clamp(_currentQuarters - i * HealthSystem.QuartersPerHeart,
                                           0, HealthSystem.QuartersPerHeart);
                _hearts[i].SetFill(quarters / (float)HealthSystem.QuartersPerHeart);
            }
        }

        private void RefreshWheels()
        {
            if (_staminaRings == null) return;

            Color colour = _exhausted ? _exhaustedColour : _staminaColour;

            for (int i = 0; i < _staminaRings.Length; i++)
            {
                bool ringExists = _staminaMax - i > 0.001f;

                if (_staminaRingBackings != null && i < _staminaRingBackings.Length && _staminaRingBackings[i] != null)
                    _staminaRingBackings[i].enabled = ringExists;

                Image ring = _staminaRings[i];
                if (ring == null) continue;

                ring.enabled = ringExists;
                if (!ringExists) continue;

                ring.fillAmount = Mathf.Clamp01(_stamina - i);
                ring.color = colour;
            }

            if (_wheelGroup != null)
            {
                bool idleFull = _hideWheelWhenFull && !_exhausted && _stamina >= _staminaMax - 0.001f;
                _wheelGroup.alpha = Mathf.MoveTowards(_wheelGroup.alpha, idleFull ? 0f : 1f,
                                                      Time.unscaledDeltaTime * 4f);
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            bool tookDamage = current < _currentQuarters;
            bool capacityChanged = max != _maxQuarters;

            _currentQuarters = current;
            _maxQuarters = max;

            if (capacityChanged) RebuildHearts();
            else RefreshHearts();

            if (tookDamage) _flashUntil = Time.unscaledTime + 0.35f;
        }

        private void OnStaminaChanged(float current, float max, bool exhausted)
        {
            _stamina = current;
            _staminaMax = max;
            _exhausted = exhausted;
        }

        private void OnAnnouncement(string message, float duration)
        {
            if (_announcementText == null) return;
            _announcementText.text = message;
            _announcementUntil = Time.unscaledTime + duration;
        }

        private void OnPrompt(string prompt)
        {
            if (_promptText != null) _promptText.text = string.IsNullOrEmpty(prompt) ? "" : prompt;
        }

        private void OnRegionEntered(int region, int seed) => OnAnnouncement($"Region {region}", 3f);

        private void OnPlayerDied()
        {
            OnAnnouncement("You have fallen", 2.5f);
            _flashUntil = Time.unscaledTime + 1.2f;
        }

        private void Update()
        {
            ApplyResponsiveLayout(force: false);
            RefreshWheels();
            UpdateOverlays();
            UpdateStats();
            UpdateObjective();
        }

        private void UpdateOverlays()
        {
            if (_damageFlash != null)
            {
                Color c = _damageFlash.color;
                c.a = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / 0.35f) * 0.35f;
                _damageFlash.color = c;
            }

            if (_announcementText != null)
            {
                Color a = _announcementText.color;
                a.a = Mathf.Clamp01((_announcementUntil - Time.unscaledTime) / 0.5f);
                _announcementText.color = a;
            }

            bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
            if (_pausePanel != null && _pausePanel.activeSelf != paused) _pausePanel.SetActive(paused);
        }

        private void UpdateStats()
        {
            _sampleFrames++;
            _sampleTime += Time.unscaledDeltaTime;
            _worstFrame = Mathf.Max(_worstFrame, Time.unscaledDeltaTime);
            _sampleTimer += Time.unscaledDeltaTime;

            if (_sampleTimer >= StatsSampleWindow)
            {
                _displayFps = _sampleTime > 0f ? _sampleFrames / _sampleTime : 0f;
                _displayWorstMs = _worstFrame * 1000f;
                _sampleTimer = _sampleTime = _worstFrame = 0f;
                _sampleFrames = 0;
            }

            if (_statsText == null) return;

            PlayerController player = PlayerController.Instance;
            GameManager gm = GameManager.Instance;

            _statsText.text =
                $"FPS {_displayFps:0}   worst {_displayWorstMs:0.0} ms\n" +
                $"{Screen.width}x{Screen.height}  ratio {(Screen.width / (float)Screen.height):0.00}\n" +
                $"State  {(player != null ? player.State.ToString() : "-")}\n" +
                $"Speed  {(player != null ? player.PlanarSpeed : 0f):0.0} m/s\n" +
                $"Hearts {_currentQuarters / 4f:0.##} / {_maxQuarters / 4f:0.##}\n" +
                $"Wheels {_stamina:0.00} / {_staminaMax:0.00}\n" +
                (gm != null ? $"Region {gm.RegionNumber}   Orbs {gm.OrbsCollected}/{gm.OrbsRequired}" : "");
        }

        private void UpdateObjective()
        {
            if (_objectiveText == null) return;

            PlayerController player = PlayerController.Instance;
            GameManager gm = GameManager.Instance;

            if (player == null || gm == null ||
                !gm.TryGetNearestObjective(player.transform.position, out Vector3 position, out string label))
            {
                _objectiveText.text = "";
                return;
            }

            float distance = Vector3.Distance(player.transform.position, position);
            float rise = position.y - player.transform.position.y;

            string vertical = Mathf.Abs(rise) < 3f ? "" : rise > 0f ? $"  +{rise:0} m up" : $"  {rise:0} m down";
            _objectiveText.text = $"{label}   {distance:0} m{vertical}";
        }

        private void ApplyResponsiveLayout(bool force)
        {
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;

            if (!force && screen == _lastScreen && safe == _lastSafeArea) return;
            _lastScreen = screen;
            _lastSafeArea = safe;

            if (_safeArea != null)
            {
                Vector2 min = safe.position;
                Vector2 max = safe.position + safe.size;
                min.x /= Mathf.Max(1f, screen.x);
                min.y /= Mathf.Max(1f, screen.y);
                max.x /= Mathf.Max(1f, screen.x);
                max.y /= Mathf.Max(1f, screen.y);
                _safeArea.anchorMin = min;
                _safeArea.anchorMax = max;
                _safeArea.offsetMin = Vector2.zero;
                _safeArea.offsetMax = Vector2.zero;
            }

            bool portrait = screen.y > screen.x;

            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.referenceResolution = portrait
                    ? new Vector2(ReferenceResolution.y, ReferenceResolution.x)
                    : ReferenceResolution;
                _scaler.matchWidthOrHeight = portrait ? 0f : 0.5f;
            }

            if (_staminaContainer != null)
            {
                if (portrait)
                {
                    _staminaContainer.anchorMin = _staminaContainer.anchorMax = _staminaContainer.pivot = new Vector2(1f, 1f);
                    _staminaContainer.anchoredPosition = WheelPortraitPosition;
                }
                else
                {
                    _staminaContainer.anchorMin = _staminaContainer.anchorMax = _staminaContainer.pivot = new Vector2(0.5f, 0.5f);
                    _staminaContainer.anchoredPosition = WheelLandscapePosition;
                }
            }

            if (_statsText != null)
            {
                Transform panel = _statsText.transform.parent != null ? _statsText.transform.parent : _statsText.transform;
                panel.gameObject.SetActive(ShowStats && screen.x >= StatsMinScreenWidth);
            }
        }
    }
}
