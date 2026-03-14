using System.Collections.Generic;
using UnityEngine;

public static class CreatureLevelUpSignal
{
    private struct PulseState
    {
        public float startedAt;
        public float duration;
    }

    private const float DefaultDuration = 1.8f;
    private const float PendingReleaseDelayAfterBattleSeconds = 2.0f;
    private static readonly Dictionary<string, PulseState> ActivePulses = new Dictionary<string, PulseState>();
    private static readonly Dictionary<string, PulseState> PendingPulses = new Dictionary<string, PulseState>();

    public static void Notify(CreatureInstance instance, float duration = DefaultDuration)
    {
        string key = ResolveKey(instance);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        float clampedDuration = Mathf.Max(0.1f, duration);
        if (BattleSystem.IsEngagedBattleActive)
        {
            PendingPulses[key] = new PulseState
            {
                startedAt = 0f,
                duration = clampedDuration
            };
            ActivePulses.Remove(key);
            return;
        }

        ActivePulses[key] = new PulseState
        {
            startedAt = Time.unscaledTime,
            duration = clampedDuration
        };
    }

    public static bool TryGetPulse01(CreatureInstance instance, out float pulse01)
    {
        pulse01 = 0f;
        string key = ResolveKey(instance);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (BattleSystem.IsEngagedBattleActive)
        {
            if (ActivePulses.TryGetValue(key, out PulseState activeDuringBattle))
            {
                float elapsedDuringBattle = Mathf.Max(0f, Time.unscaledTime - activeDuringBattle.startedAt);
                float remaining = Mathf.Max(0.1f, activeDuringBattle.duration - elapsedDuringBattle);
                PendingPulses[key] = new PulseState
                {
                    startedAt = 0f,
                    duration = remaining
                };
                ActivePulses.Remove(key);
            }
            return false;
        }

        if (PendingPulses.TryGetValue(key, out PulseState pending))
        {
            float pendingDelay = Mathf.Max(0f, PendingReleaseDelayAfterBattleSeconds);
            if (pending.startedAt <= 0f)
            {
                pending.startedAt = Time.unscaledTime;
                PendingPulses[key] = pending;
                return false;
            }

            if ((Time.unscaledTime - pending.startedAt) < pendingDelay)
            {
                return false;
            }

            ActivePulses[key] = new PulseState
            {
                startedAt = Time.unscaledTime,
                duration = Mathf.Max(0.1f, pending.duration)
            };
            PendingPulses.Remove(key);
        }

        if (!ActivePulses.TryGetValue(key, out PulseState pulse))
        {
            return false;
        }

        float elapsed = Time.unscaledTime - pulse.startedAt;
        if (elapsed < 0f)
        {
            elapsed = 0f;
        }

        if (elapsed >= pulse.duration)
        {
            ActivePulses.Remove(key);
            return false;
        }

        pulse01 = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, pulse.duration));
        return true;
    }

    private static string ResolveKey(CreatureInstance instance)
    {
        if (instance == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(instance.creatureUID))
        {
            return instance.creatureUID.Trim();
        }

        if (!string.IsNullOrWhiteSpace(instance.definitionID))
        {
            return "def:" + instance.definitionID.Trim();
        }

        return string.Empty;
    }
}
