using UnityEngine;

/// <summary>
/// Keeps score for the dirt patches and provides the "juice": mud splashes, sparkles and sounds.
/// One per scene.
/// </summary>
public class PatchManager : MonoBehaviour
{
    public static PatchManager Instance { get; private set; }

    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    public int Total => patches == null ? 0 : patches.Length;
    public int Removed { get; private set; }
    public float Progress => Total == 0 ? 0f : (float)Removed / Total;
    public bool IsComplete => Total > 0 && Removed >= Total;

    DirtPatch[] patches;
    ParticleSystem mud, sparkle;
    AudioSource sfx;
    AudioClip popClip, chimeClip;

    void Awake()
    {
        Instance = this;
        mud = CreateFx("MudSplash", 1.3f, true);
        sparkle = CreateFx("Sparkle", -0.1f, false);
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        popClip = ProceduralAudio.Pop();
        chimeClip = ProceduralAudio.Chime();
    }

    void Start()
    {
        patches = FindObjectsByType<DirtPatch>(FindObjectsSortMode.None);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Little bits of mud flicking off while a patch is being sprayed.</summary>
    public void Splash(Vector3 point, Vector3 normal, Color colour)
    {
        if (Random.value > 0.5f) return;
        Burst(mud, point, normal, colour, 2, 2.5f, 0.012f, 0.03f);
    }

    /// <summary>Water bouncing off a patch that's too tough for the nozzle.</summary>
    public void Deflect(Vector3 point, Vector3 normal)
    {
        if (Random.value > 0.4f) return;
        Burst(sparkle, point, normal, new Color(0.8f, 0.9f, 1f), 1, 3f, 0.01f, 0.025f);
    }

    /// <summary>A patch has been washed off.</summary>
    public void Popped(DirtPatch patch)
    {
        Removed++;
        Vector3 p = patch.transform.position, n = patch.transform.up;
        Burst(mud, p, n, patch.colour, 25, 3.5f, 0.015f, 0.045f);
        Burst(sparkle, p, n, Color.white, 8, 1.5f, 0.01f, 0.03f);

        sfx.pitch = Random.Range(0.85f, 1.2f);
        sfx.PlayOneShot(popClip, sfxVolume);
        if (IsComplete)
        {
            sfx.pitch = 1f;
            sfx.PlayOneShot(chimeClip, sfxVolume);
        }
    }

    public void ResetAll()
    {
        if (patches == null) return;
        foreach (var p in patches) p.ResetPatch();
        Removed = 0;
    }

    static void Burst(ParticleSystem ps, Vector3 point, Vector3 normal, Color colour, int count, float speed, float minSize, float maxSize)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
            Color c = colour * Random.Range(0.8f, 1.1f);
            c.a = 1f;
            ep.position = point + normal * 0.01f;
            ep.velocity = dir * speed * Random.Range(0.4f, 1f);
            ep.startColor = c;
            ep.startSize = Random.Range(minSize, maxSize);
            ep.startLifetime = Random.Range(0.4f, 0.9f);
            ps.Emit(ep, 1);
        }
    }

    ParticleSystem CreateFx(string fxName, float gravity, bool collide)
    {
        var go = new GameObject(fxName);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 2000;

        var emission = ps.emission;
        emission.enabled = false;           // we emit by hand with Burst()
        var shape = ps.shape;
        shape.enabled = false;

        var size = ps.sizeOverLifetime;     // shrink away rather than vanish
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        if (collide)
        {
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = 0.1f;
            collision.dampen = 0.8f;
            collision.radiusScale = 0.5f;
        }

        go.GetComponent<ParticleSystemRenderer>().material = SprayNozzle.CreateSoftParticleMaterial();
        ps.Play();
        return ps;
    }
}
