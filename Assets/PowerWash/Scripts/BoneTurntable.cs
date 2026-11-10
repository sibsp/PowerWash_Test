using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Q / E spin the bone, Z / C tip it towards / away from you, so every side can be washed.</summary>
public class BoneTurntable : MonoBehaviour
{
    public float degreesPerSecond = 90f;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float spin = 0f, tilt = 0f;
        if (kb.qKey.isPressed) spin += 1f;
        if (kb.eKey.isPressed) spin -= 1f;
        if (kb.zKey.isPressed) tilt += 1f;
        if (kb.cKey.isPressed) tilt -= 1f;

        float step = degreesPerSecond * Time.deltaTime;
        if (spin != 0f) transform.Rotate(Vector3.up, spin * step, Space.World);
        if (tilt != 0f)
        {
            Vector3 axis = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            transform.Rotate(axis, tilt * step, Space.World);
        }
    }
}
