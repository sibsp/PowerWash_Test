using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Bare-bones on-screen info: crosshair, % clean, nozzle, controls. R resets the dirt.</summary>
public class PowerWashHUD : MonoBehaviour
{
    public DirtSurface target;
    public SprayNozzle nozzle;
    public PatchManager patches;

    GUIStyle big, small, centre, warn;

    void Start()
    {
        if (target == null) target = FindFirstObjectByType<DirtSurface>();
        if (nozzle == null) nozzle = FindFirstObjectByType<SprayNozzle>();
        if (patches == null) patches = FindFirstObjectByType<PatchManager>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (target != null) target.ResetDirt();
            if (patches != null) patches.ResetAll();
        }
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            centre = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            warn = new GUIStyle(centre) { fontSize = 24 };
            warn.normal.textColor = new Color(1f, 0.75f, 0.2f);
        }

        GUI.Label(new Rect(Screen.width / 2f - 10, Screen.height / 2f - 14, 20, 28), "+", big);

        if (patches != null)
        {
            GUI.Label(new Rect(20, 15, 500, 40), $"Dirt removed: {patches.Removed} / {patches.Total}", big);
            if (patches.IsComplete)
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 60), "BONE CLEAN!  (R to reset)", centre);
        }
        else if (target != null)
        {
            GUI.Label(new Rect(20, 15, 400, 40), $"Clean: {target.PercentClean * 100f:0}%", big);
            if (target.IsComplete)
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 60), "BONE CLEAN!  (R to reset)", centre);
        }
        if (nozzle != null)
        {
            GUI.Label(new Rect(20, 55, 400, 30), $"Nozzle: {nozzle.CurrentTip.name}  (strength {nozzle.CurrentTip.strength:0.0})", small);
            if (nozzle.TooToughForNozzle)
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 40), "Too tough! Try a stronger nozzle", warn);
        }

        GUI.Label(new Rect(20, Screen.height - 50, 1000, 40),
            "LMB spray · 1/2/3 or scroll: nozzle · WASD move · Ctrl crouch · Q/E spin bone · Z/C tip bone · R reset · G show rays · Esc free mouse",
            small);
    }
}
