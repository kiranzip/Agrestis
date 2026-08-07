using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Agrestis.Player;
using Agrestis.World;

namespace Agrestis.Core
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("This region")]
        [Tooltip("Orbs needed to open the gate. 0 counts the shrines in the scene.")]
        public int OrbsThisRegion;
        [Tooltip("Region number, also used for enemy difficulty.")]
        public int RegionNumber = 1;
        [Tooltip("Scene loaded through the gate. Empty for the last region.")]
        public string NextSceneName = "";

        [Header("Rules")]
        [Tooltip("Orbs per upgrade.")]
        public int OrbsPerUpgrade = 2;
        [Tooltip("Difficulty added per region.")]
        public float DifficultyPerRegion = 0.35f;

        [Header("Death")]
        public float RespawnDelay = 2.5f;

        [Header("Scene references")]
        [Tooltip("Gate for this region. Found automatically if empty.")]
        public GateOfPassage Gate;
        [Tooltip("Start and default respawn point.")]
        public Transform DefaultRespawnPoint;

        public int OrbsCollected { get; private set; }
        public int TotalOrbsEver { get; private set; }
        public int HeartUpgrades { get; private set; }
        public int StaminaUpgrades { get; private set; }
        public bool IsPaused { get; private set; }
        public bool GateOpen => Gate != null && Gate.IsOpen;

        public float DifficultyScalar => 1f + (RegionNumber - 1) * DifficultyPerRegion;
        public int OrbsRequired => OrbsThisRegion;

        private PlayerController _player;
        private Vector3 _respawnPoint;
        private bool _busy;
        private readonly List<Shrine> _shrines = new List<Shrine>();
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnEnable() => GameEvents.PlayerDied += HandlePlayerDied;
        private void OnDisable() => GameEvents.PlayerDied -= HandlePlayerDied;

        private void Start() => BindToCurrentScene();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindToCurrentScene();

        private void BindToCurrentScene()
        {
            _busy = false;
            OrbsCollected = 0;

            _shrines.Clear();
            _shrines.AddRange(FindObjectsByType<Shrine>(FindObjectsSortMode.None));

            if (OrbsThisRegion <= 0) OrbsThisRegion = Mathf.Max(1, _shrines.Count);

            if (Gate == null) Gate = FindFirstObjectByType<GateOfPassage>();

            _player = PlayerController.Instance;
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();

            if (DefaultRespawnPoint == null)
            {
                GameObject start = GameObject.Find("PlayerStart");
                if (start != null) DefaultRespawnPoint = start.transform;
            }

            _respawnPoint = DefaultRespawnPoint != null
                ? DefaultRespawnPoint.position
                : _player != null ? _player.transform.position : Vector3.zero;

            ApplyUpgradesToPlayer();

            GameEvents.RaiseOrbsChanged(OrbsCollected, OrbsThisRegion);
            GameEvents.RaiseLevelGenerated(RegionNumber, 0);
        }

        public void RegisterPlayer(PlayerController player)
        {
            _player = player;
            ApplyUpgradesToPlayer();
        }

        public void CollectOrb()
        {
            OrbsCollected++;
            TotalOrbsEver++;
            GameEvents.RaiseOrbsChanged(OrbsCollected, OrbsThisRegion);

            if (TotalOrbsEver % Mathf.Max(1, OrbsPerUpgrade) == 0)
                GrantUpgrade();
            else
                GameEvents.RaiseAnnouncement($"Spirit Orb obtained  ({OrbsCollected}/{OrbsThisRegion})", 3f);

            if (OrbsCollected >= OrbsThisRegion) OpenGate();
        }

        private void GrantUpgrade()
        {
            bool giveHeart = HeartUpgrades <= StaminaUpgrades;

            if (giveHeart)
            {
                HeartUpgrades++;
                _player?.Health?.AddHeartContainer();
                GameEvents.RaiseAnnouncement("Heart Container gained", 3.5f);
            }
            else
            {
                StaminaUpgrades++;
                _player?.Stamina?.AddWheelSegment();
                GameEvents.RaiseAnnouncement("Stamina Vessel gained", 3.5f);
            }
        }

        private void ApplyUpgradesToPlayer()
        {
            if (_player == null) return;

            for (int i = 0; i < HeartUpgrades; i++) _player.Health?.AddHeartContainer();
            for (int i = 0; i < StaminaUpgrades; i++) _player.Stamina?.AddWheelSegment();
        }

        private void OpenGate()
        {
            if (Gate == null)
            {
                GameEvents.RaiseAnnouncement("All orbs claimed", 4f);
                return;
            }

            if (Gate.IsOpen) return;
            Gate.Open();
            GameEvents.RaiseAnnouncement("All orbs claimed - the Gate of Passage has opened", 5f);
        }

        public void AdvanceLevel()
        {
            if (_busy) return;

            if (string.IsNullOrEmpty(NextSceneName))
            {
                GameEvents.RaiseAnnouncement("You have crossed the last gate. Agrestis is yours.", 6f);
                return;
            }

            StartCoroutine(LoadNextRegion());
        }

        private IEnumerator LoadNextRegion()
        {
            _busy = true;
            GameEvents.RaiseAnnouncement("Crossing the gate...", 2f);
            yield return new WaitForSecondsRealtime(1.4f);

            SceneManager.LoadScene(NextSceneName, LoadSceneMode.Single);
        }

        public void SetRespawnPoint(Vector3 point) => _respawnPoint = point;
        public Vector3 GetRespawnPoint() => _respawnPoint;

        private void HandlePlayerDied()
        {
            if (_busy) return;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            _busy = true;
            GameEvents.RaiseAnnouncement("You fell. Rising again at the last shrine...", RespawnDelay);
            yield return new WaitForSecondsRealtime(RespawnDelay);

            if (_player == null) _player = PlayerController.Instance;

            if (_player != null)
            {
                _player.Teleport(_respawnPoint);
                _player.Health?.FullHeal();
                _player.Stamina?.Refill();
                _player.Revive();
            }

            _busy = false;
        }

        public void TogglePause()
        {
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsPaused;
        }

        public bool TryGetNearestObjective(Vector3 from, out Vector3 position, out string label)
        {
            float best = float.MaxValue;
            position = Vector3.zero;
            label = null;

            foreach (Shrine shrine in _shrines)
            {
                if (shrine == null || shrine.Activated) continue;

                float d = (shrine.transform.position - from).sqrMagnitude;
                if (d >= best) continue;

                best = d;
                position = shrine.transform.position;
                label = "Shrine";
            }

            if (label == null && Gate != null)
            {
                position = Gate.transform.position;
                label = "Gate of Passage";
            }

            return label != null;
        }
    }
}
