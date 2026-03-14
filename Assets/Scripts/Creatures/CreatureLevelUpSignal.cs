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
    private static readonly Dictionary<string, PulseState> ActivePulses = new Dictionary<string, PulseState>();

    public static void Notify(CreatureInstance instance, float duration = DefaultDuration)
    {
        string key = ResolveKey(instance);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ActivePulses[key] = new PulseState
        {
            startedAt = Time.unscaledTime,
            duration = Mathf.Max(0.1f, duration)
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
