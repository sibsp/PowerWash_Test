using UnityEngine;

/// <summary>
/// Placeholder sounds made in code so the prototype needs no audio files.
/// Swap for real recordings later (e.g. from freesound.org) by assigning AudioClips instead.
/// </summary>
public static class ProceduralAudio
{
    const int Rate = 44100;

    /// <summary>One second of softened noise that loops — a passable hose hiss.</summary>
    public static AudioClip HoseLoop()
    {
        var rnd = new System.Random(1);
        var data = new float[Rate];
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.3f;          // simple low-pass filter
            data[i] = low * 0.7f;
        }
        return Make("Hose", data);
    }

    /// <summary>Short wet "splat-pop" when a patch comes off.</summary>
    public static AudioClip Pop()
    {
        var rnd = new System.Random(2);
        var data = new float[(int)(Rate * 0.2f)];
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.5f;
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, t / 0.2f) * t);
            data[i] = low * 0.6f * Mathf.Exp(-t * 35f) + thump * 0.8f * Mathf.Exp(-t * 22f);
        }
        return Make("Pop", data);
    }

    /// <summary>Three rising notes for "all clean".</summary>
    public static AudioClip Chime()
    {
        float[] notes = { 1046.5f, 1318.5f, 1568f };   // C6, E6, G6
        var data = new float[(int)(Rate * 1.1f)];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(Rate * 0.12f * n);
            for (int i = start; i < data.Length; i++)
            {
                float t = (float)(i - start) / Rate;
                data[i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * 0.3f * Mathf.Exp(-t * 4f);
            }
        }
        return Make("Chime", data);
    }

    static AudioClip Make(string clipName, float[] data)
    {
        var clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
