using System;
using UnityEngine;

namespace Agrestis.Core
{
    public static class GameEvents
    {
        public static event Action<int, int> HealthChanged;

        public static event Action<float, float, bool> StaminaChanged;
        public static event Action PlayerDied;
        public static event Action<PlayerMotionState> PlayerStateChanged;

        public static event Action<int, int> OrbsChanged;

        public static event Action<int, int> LevelGenerated;
        public static event Action<string, float> Announcement;
        public static event Action<string> InteractPromptChanged;

        public static event Action<Vector3> LoudNoiseMade;

        public static void RaiseHealthChanged(int current, int max) => HealthChanged?.Invoke(current, max);
        public static void RaiseStaminaChanged(float current, float max, bool exhausted) => StaminaChanged?.Invoke(current, max, exhausted);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerStateChanged(PlayerMotionState s) => PlayerStateChanged?.Invoke(s);
        public static void RaiseOrbsChanged(int collected, int required) => OrbsChanged?.Invoke(collected, required);
        public static void RaiseLevelGenerated(int level, int seed) => LevelGenerated?.Invoke(level, seed);
        public static void RaiseAnnouncement(string message, float duration = 3f) => Announcement?.Invoke(message, duration);
        public static void RaiseInteractPrompt(string prompt) => InteractPromptChanged?.Invoke(prompt);
        public static void RaiseLoudNoise(Vector3 position) => LoudNoiseMade?.Invoke(position);

        public static void ResetAll()
        {
            HealthChanged = null;
            StaminaChanged = null;
            PlayerDied = null;
            PlayerStateChanged = null;
            OrbsChanged = null;
            LevelGenerated = null;
            Announcement = null;
            InteractPromptChanged = null;
            LoudNoiseMade = null;
        }
    }

    public enum PlayerMotionState
    {
        Idle,
        Walking,
        Sprinting,
        Airborne,
        Climbing,
        Swimming,
        Gliding,
        Exhausted,
        Dead
    }
}
