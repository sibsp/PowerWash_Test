using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold left mouse to spray. 1 / 2 / 3 or the scroll wheel change nozzle. G toggles ray debugging.
///
/// Works with both versions of the game:
///  - Texture version (DirtSurface): fires a cone of rays; each ray that hits removes dirt at that texture pixel.
///  - Patch version (DirtPatch): finds the point under the crosshair and damages every patch within a
///    sphere around it. Wider nozzles = bigger sphere but less damage. Simple, and players can't tell.
/// The particle system is only for show in both.
/// </summary>
public class SprayNozzle : MonoBehaviour
{
    [System.Serializable]
    public class NozzleTip
    {
        public string name = "Nozzle";
        [Tooltip("Half-angle of the spray cone in degrees")] public float coneAngle = 5f;
        [Tooltip("Texture version: radius of each water impact, in dirt-texture pixels")] public float splashRadius = 8f;
        [Tooltip("Texture version: dirt removed per second by each ray at close range")] public float power = 1f;
        [Tooltip("Patch version: damage per second to each patch in the spray")] public float patchPower = 1.5f;
        [Tooltip("0-1. Can only clean dirt whose toughness is at or below this")]
        [Range(0f, 1f)] public float strength = 1f;
    }

    [Tooltip("Where the spray is aimed from — usually the camera")]
    public Transform aim;
    [Tooltip("End of the lance, where the water comes out")]
    public Transform tip;

    public NozzleTip[] tips =
    {
        new NozzleTip { name = "0° Pencil", coneAngle = 1.5f, splashRadius = 6f,  power = 1.6f,  patchPower = 3f,   strength = 1f },
        new NozzleTip { name = "15° Fan",   coneAngle = 5f,   splashRadius = 9f,  power = 0.7f,  patchPower = 1.5f, strength = 0.6f },
        new NozzleTip { name = "40° Wide",  coneAngle = 12f,  splashRadius = 12f, power = 0.35f, patchPower = 0.8f, strength = 0.3f },
    };
    public int currentTip = 1;

    [Header("Spray")]
    public int raysPerFrame = 12;
    public float maxRange = 8f;
    [Tooltip("Full power up to this distance, fading to nothing at max range")]
    public float fullPowerRange = 2.5f;
    public LayerMask hitMask = ~0;

    [Header("Sound")]
    public bool hoseSound = true;
    [Range(0f, 1f)] public float hoseVolume = 0.3f;

    [Header("Debug")]
    [Tooltip("Draw the spray rays. Visible in the Scene view, or in the Game view with Gizmos turned on. Toggle in Play with G.")]
    public bool showRays = true;
    [Tooltip("How long each ray stays on screen, in seconds. 0 = one frame. Higher values show the cone filling in.")]
    public float rayLingerTime = 0.15f;
    public Color cleaningHitColour = Color.cyan;    // hit something cleanable
    public Color otherHitColour = Color.yellow;     // hit something that can't be cleaned (or too tough)
    public Color missColour = new Color(1f, 0f, 0f, 0.4f); // hit nothing within range
    public Color patchAreaColour = Color.magenta;   // patch version: the damage sphere

    public bool IsSpraying { get; private set; }
    /// <summary>True when most of the spray is hitting dirt too tough for the current nozzle.</summary>
    public bool TooToughForNozzle { get; private set; }
    public NozzleTip CurrentTip => tips[currentTip];

    ParticleSystem water;
    AudioSource hose;
    float toughTimer;
    readonly Collider[] overlaps = new Collider[64];

    void Start()
    {
        if (aim == null && Camera.main != null) aim = Camera.main.transform;
        if (tip == null) tip = transform;
        water = CreateWaterParticles();

        if (hoseSound)
        {
            hose = gameObject.AddComponent<AudioSource>();
            hose.clip = ProceduralAudio.HoseLoop();
            hose.loop = true;
            hose.volume = 0f;
            hose.Play();
        }
        SetTip(currentTip);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetTip(0);
        if (kb.digit2Key.wasPressedThisFrame) SetTip(1);
        if (kb.digit3Key.wasPressedThisFrame) SetTip(2);
        if (kb.gKey.wasPressedThisFrame) showRays = !showRays;
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0.01f) SetTip(currentTip - 1);
        else if (scroll < -0.01f) SetTip(currentTip + 1);

        IsSpraying = mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        var emission = water.emission;
        emission.enabled = IsSpraying;
        UpdateHoseSound();
        if (!IsSpraying) { TooToughForNozzle = false; toughTimer = 0f; return; }

        // Point the water at whatever is under the crosshair.
        bool hasCentre = Physics.Raycast(aim.position, aim.forward, out RaycastHit centre, maxRange, hitMask, QueryTriggerInteraction.Ignore);
        Vector3 aimPoint = hasCentre ? centre.point : aim.position + aim.forward * maxRange;
        tip.rotation = Quaternion.LookRotation(aimPoint - tip.position);

        bool tooTough = SprayTextureSurfaces();
        if (hasCentre && SprayPatches(centre)) tooTough = true;

        // Show the "too tough" hint once the spray has been bouncing off for a moment.
        toughTimer = tooTough ? toughTimer + Time.deltaTime : 0f;
        TooToughForNozzle = toughTimer > 0.4f;
    }

    // ---------------- Texture version ----------------

    /// <summary>Cone of rays for DirtSurface objects. Returns true if most hits were too tough.</summary>
    bool SprayTextureSurfaces()
    {
        float spread = Mathf.Tan(CurrentTip.coneAngle * Mathf.Deg2Rad);
        float perRay = CurrentTip.power * Time.deltaTime;
        int surfaceHits = 0, blockedHits = 0;

        for (int i = 0; i < raysPerFrame; i++)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            Vector3 dir = (aim.forward + aim.right * r.x + aim.up * r.y).normalized;
            if (!Physics.Raycast(aim.position, dir, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (showRays) Debug.DrawRay(aim.position, dir * maxRange, missColour, rayLingerTime);
                continue;
            }

            float distanceFactor = 1f - Mathf.InverseLerp(fullPowerRange, maxRange, hit.distance);
            var surface = hit.collider.GetComponentInParent<DirtSurface>();
            if (surface == null)
            {
                if (showRays)
                {
                    // Patches are cleanable too (handled in SprayPatches), so colour them as cleaning hits.
                    bool isPatch = hit.collider.GetComponentInParent<DirtPatch>() != null;
                    Color c = isPatch ? Color.Lerp(missColour, cleaningHitColour, distanceFactor) : otherHitColour;
                    Debug.DrawLine(aim.position, hit.point, c, rayLingerTime);
                }
                continue;
            }

            bool blocked = surface.Clean(hit.textureCoord, CurrentTip.splashRadius, perRay * distanceFactor, CurrentTip.strength);
            surfaceHits++;
            if (blocked) blockedHits++;

            if (showRays)
            {
                // Ray plus a small cross at the impact. Fades with distance (weaker hits); yellow if too tough.
                Color c = blocked ? otherHitColour : Color.Lerp(missColour, cleaningHitColour, distanceFactor);
                Debug.DrawLine(aim.position, hit.point, c, rayLingerTime);
                DrawImpact(hit.point, hit.normal, 0.02f, c);
            }
        }
        return surfaceHits > 0 && blockedHits > surfaceHits / 2;
    }

    // ---------------- Patch version ----------------

    /// <summary>
    /// Damages every DirtPatch inside a sphere around the crosshair point.
    /// The sphere is as wide as the spray cone at that distance. Returns true if it only hit tough patches.
    /// </summary>
    bool SprayPatches(RaycastHit centre)
    {
        float distanceFactor = 1f - Mathf.InverseLerp(fullPowerRange, maxRange, centre.distance);
        float radius = Mathf.Max(0.04f, Mathf.Tan(CurrentTip.coneAngle * Mathf.Deg2Rad) * centre.distance);
        float damage = CurrentTip.patchPower * Time.deltaTime * distanceFactor;

        int count = Physics.OverlapSphereNonAlloc(centre.point, radius, overlaps, hitMask, QueryTriggerInteraction.Ignore);
        bool anyDamaged = false, anyBlocked = false;
        for (int i = 0; i < count; i++)
        {
            var patch = overlaps[i].GetComponentInParent<DirtPatch>();
            if (patch == null) continue;
            Vector3 point = overlaps[i].ClosestPoint(centre.point);
            if (patch.Hit(damage, CurrentTip.strength, point)) anyBlocked = true;
            else anyDamaged = true;
        }

        if (showRays && count > 0)
            DrawCircle(centre.point, centre.normal, radius, anyBlocked && !anyDamaged ? otherHitColour : patchAreaColour);

        return anyBlocked && !anyDamaged;
    }

    // ---------------- Nozzle, sound, visuals ----------------

    public void SetTip(int index)
    {
        currentTip = (index % tips.Length + tips.Length) % tips.Length;
        if (water != null)
        {
            var shape = water.shape;
            shape.angle = CurrentTip.coneAngle;
        }
    }

    void UpdateHoseSound()
    {
        if (hose == null) return;
        hose.volume = Mathf.MoveTowards(hose.volume, IsSpraying ? hoseVolume : 0f, Time.deltaTime * 3f);
        // Narrow nozzle sounds higher and harsher, wide nozzle lower and softer
        float t = tips.Length > 1 ? (float)currentTip / (tips.Length - 1) : 0f;
        hose.pitch = Mathf.Lerp(1.3f, 0.8f, t);
    }

    ParticleSystem CreateWaterParticles()
    {
        var go = new GameObject("WaterSpray");
        go.transform.SetParent(tip, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.5f;
        main.startSpeed = 16f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new Color(0.8f, 0.9f, 1f, 0.55f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;

        var emission = ps.emission;
        emission.rateOverTime = 500f;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.005f;
        shape.angle = CurrentTip.coneAngle;

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.bounce = 0.2f;
        collision.dampen = 0.6f;
        collision.lifetimeLoss = 0.5f;
        collision.radiusScale = 0.5f;

        go.GetComponent<ParticleSystemRenderer>().material = CreateSoftParticleMaterial();
        ps.Play();
        return ps;
    }

    /// <summary>Transparent material with a soft round dot, for water and mud particles.</summary>
    public static Material CreateSoftParticleMaterial()
    {
        // Sprites/Default is transparent and uses particle colours; works in URP.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var mat = new Material(shader) { name = "SoftParticle" };

        const int size = 32;
        var dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
            float a = Mathf.Clamp01(1f - d);
            dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        dot.Apply();
        mat.mainTexture = dot;
        return mat;
    }

    // ---------------- Debug drawing ----------------

    void DrawImpact(Vector3 point, Vector3 normal, float size, Color colour)
    {
        // Two short lines lying flat on the surface, plus the surface normal.
        Vector3 a = Vector3.Cross(normal, Vector3.up);
        if (a.sqrMagnitude < 0.001f) a = Vector3.Cross(normal, Vector3.right);
        a = a.normalized * size;
        Vector3 b = Vector3.Cross(normal, a).normalized * size;
        Debug.DrawLine(point - a, point + a, colour, rayLingerTime);
        Debug.DrawLine(point - b, point + b, colour, rayLingerTime);
        Debug.DrawRay(point, normal * size * 2f, Color.white, rayLingerTime);
    }

    void DrawCircle(Vector3 centre, Vector3 normal, float radius, Color colour)
    {
        Vector3 a = Vector3.Cross(normal, Vector3.up);
        if (a.sqrMagnitude < 0.001f) a = Vector3.Cross(normal, Vector3.right);
        a.Normalize();
        Vector3 b = Vector3.Cross(normal, a).normalized;
        const int segments = 20;
        Vector3 prev = centre + a * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = centre + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * radius;
            Debug.DrawLine(prev, next, colour, rayLingerTime);
            prev = next;
        }
    }

    // Draws the cone outline in the Scene view whenever the camera is selected, even outside Play mode.
    void OnDrawGizmosSelected()
    {
        if (!showRays || tips == null || tips.Length == 0) return;
        Transform a = aim != null ? aim : transform;
        float angle = tips[Mathf.Clamp(currentTip, 0, tips.Length - 1)].coneAngle;
        float radius = Mathf.Tan(angle * Mathf.Deg2Rad) * maxRange;
        Vector3 end = a.position + a.forward * maxRange;

        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        const int segments = 24;
        Vector3 prev = end + a.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = end + (a.right * Mathf.Cos(t) + a.up * Mathf.Sin(t)) * radius;
            Gizmos.DrawLine(prev, next);
            if (i % 6 == 0) Gizmos.DrawLine(a.position, next);
            prev = next;
        }

        // Full-power range marker
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(a.position + a.forward * fullPowerRange, 0.05f);
    }
}
