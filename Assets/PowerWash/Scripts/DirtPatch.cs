using UnityEngine;

/// <summary>
/// "Smoke and mirrors" dirt: a clump of mud stuck on the bone. The clean bone is underneath the whole time.
/// Spray it to wear it down — it shrinks and wobbles — and when its health runs out it pops off.
/// Place as many as you like by hand: it's just an object with a mesh and a collider (any type).
/// </summary>

public class DirtPatch : MonoBehaviour
{
    [Tooltip("0-1. Nozzle strength needed to shift it. Wide 0.3, Fan 0.6, Pencil 1.0")]
    [Range(0f, 1f)] public float requiredStrength = 0.3f;
    [Tooltip("How much spraying it takes to remove")]
    public float durability = 1.5f;
    [Tooltip("Colour of the mud that splashes off. The patch's own look comes from its material.")]
    public Color colour = new Color(0.55f, 0.42f, 0.28f);

    public float Health01 => Mathf.Clamp01(health / durability);
    public bool IsRemoved { get; private set; }

    float health;
    float wobble;
    Vector3 baseScale;
    Renderer rend;
    Collider col;

    void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
        health = durability;
    }

    /// <summary>Called by the nozzle. Returns true if this nozzle is too weak for this patch.</summary>
    public bool Hit(float damage, float nozzleStrength, Vector3 point)
    {
        if (IsRemoved) return false;

        Vector3 normal = transform.up;
        if (nozzleStrength + 0.001f < requiredStrength)
        {
            if (PatchManager.Instance != null) PatchManager.Instance.Deflect(point, normal);
            wobble = Mathf.Max(wobble, 0.3f);
            return true;
        }

        health -= damage;
        wobble = 1f;
        if (PatchManager.Instance != null) PatchManager.Instance.Splash(point, normal, colour);
        if (health <= 0f) Remove();
        return false;
    }

    void Update()
    {
        if (IsRemoved) return;
        // Shrinks as it's washed (flattens faster than it narrows) and wobbles while being hit.
        float h = Health01;
        float jiggle = 1f + Mathf.Sin(Time.time * 45f) * 0.07f * wobble;
        transform.localScale = new Vector3(
            baseScale.x * Mathf.Lerp(0.45f, 1f, h) * jiggle,
            baseScale.y * Mathf.Lerp(0.15f, 1f, h),
            baseScale.z * Mathf.Lerp(0.45f, 1f, h) * jiggle);
        wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 4f);
    }

    void Remove()
    {
        IsRemoved = true;
        if (rend != null) rend.enabled = false;
        if (col != null) col.enabled = false;
        if (PatchManager.Instance != null) PatchManager.Instance.Popped(this);
    }

    public void ResetPatch()
    {
        IsRemoved = false;
        health = durability;
        wobble = 0f;
        transform.localScale = baseScale;
        if (rend != null) rend.enabled = true;
        if (col != null) col.enabled = true;
    }
}
