using UnityEngine;

/// <summary>Rotación/pulso/vaivén simple para adornos (portal, bandera, emblemas).</summary>
public class SimpleSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 30f;
    public float pulse = 0f;
    public float swayDegrees = 0f;

    Vector3 baseScale;
    Quaternion baseRot;
    float phase;

    void Start()
    {
        baseScale = transform.localScale;
        baseRot = transform.localRotation;
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        if (degreesPerSecond != 0f)
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);

        if (pulse > 0f)
            transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * 3f + phase) * pulse);

        if (swayDegrees > 0f)
            transform.localRotation = baseRot * Quaternion.AngleAxis(Mathf.Sin(Time.time * 2.2f + phase) * swayDegrees, axis);
    }
}
