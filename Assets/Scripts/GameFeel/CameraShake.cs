using UnityEngine;

/// <summary>
/// Sacudida sutil de Camera.main: desplaza localPosition en LateUpdate sólo mientras dura la sacudida
/// y restaura la posición base EXACTA al terminar. Si otro sistema movió la cámara entre frames
/// (LevelController.FitCamera), se toma esa posición nueva como base en vez de pisarla.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public const int BigEnemyHealth = 300;

    Transform camT;
    Vector3 basePos;
    Vector3 appliedOffset;
    float endTime;
    float duration;
    float amplitude;
    bool shaking;

    void OnEnable()
    {
        CombatEvents.EnemyDied += OnEnemyDied;
        CombatEvents.EnemyReachedEnd += OnEnemyReachedEnd;
    }

    void OnDisable()
    {
        CombatEvents.EnemyDied -= OnEnemyDied;
        CombatEvents.EnemyReachedEnd -= OnEnemyReachedEnd;
        StopAndRestore();
    }

    void OnEnemyDied(EnemyTD enemy)
    {
        if (enemy == null || enemy.data == null) return;
        if (enemy.data.maxHealth >= BigEnemyHealth) Shake(0.25f, 0.12f);
    }

    void OnEnemyReachedEnd(EnemyTD enemy)
    {
        Shake(0.2f, 0.08f);
    }

    /// <summary>Arranca (o refuerza) una sacudida de 'seconds' con amplitud máxima 'amp' (unidades de mundo).</summary>
    public void Shake(float seconds, float amp)
    {
        var cam = GameFeelKit.MainCamera;
        if (cam == null || seconds <= 0f || amp <= 0f) return;

        if (shaking && camT == cam.transform && camT != null)
        {
            // Quitar el offset actual antes de reconfigurar; releer la base por si la cámara se movió.
            RemoveOffset();
            basePos = camT.localPosition;
            amplitude = Mathf.Max(amp, RemainingAmplitude());
            endTime = Mathf.Max(endTime, Time.time + seconds);
            duration = Mathf.Max(duration, seconds);
            return;
        }

        if (shaking) StopAndRestore();   // sacudida sobre otra cámara: cerrarla primero

        camT = cam.transform;
        basePos = camT.localPosition;
        appliedOffset = Vector3.zero;
        amplitude = amp;
        duration = seconds;
        endTime = Time.time + seconds;
        shaking = true;
    }

    float RemainingAmplitude()
    {
        if (!shaking || duration <= 0f) return 0f;
        float remaining = Mathf.Clamp01((endTime - Time.time) / duration);
        return amplitude * remaining * remaining;
    }

    /// <summary>Vuelve la cámara a la base si el offset aplicado sigue vigente; si alguien la movió, adopta la posición nueva.</summary>
    void RemoveOffset()
    {
        if (camT == null) { appliedOffset = Vector3.zero; return; }
        Vector3 expected = basePos + appliedOffset;
        Vector3 current = camT.localPosition;
        if ((current - expected).sqrMagnitude < 1e-6f)
            camT.localPosition = basePos;
        else
            basePos = current;   // la movieron por afuera: nueva base, sin offset
        appliedOffset = Vector3.zero;
    }

    void StopAndRestore()
    {
        if (!shaking) return;
        RemoveOffset();
        shaking = false;
        camT = null;
    }

    void LateUpdate()
    {
        if (!shaking) return;
        if (camT == null)
        {
            shaking = false;
            appliedOffset = Vector3.zero;
            return;
        }

        RemoveOffset();

        float remaining = endTime - Time.time;
        if (remaining <= 0f)
        {
            // Termina exactamente en la base (RemoveOffset ya la dejó ahí).
            shaking = false;
            camT = null;
            return;
        }

        float falloff = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        float a = amplitude * falloff * falloff;
        Vector2 r = Random.insideUnitCircle * a;
        Vector3 worldOffset = camT.right * r.x + camT.up * r.y;
        Vector3 localOffset = camT.parent != null ? camT.parent.InverseTransformVector(worldOffset) : worldOffset;

        appliedOffset = localOffset;
        camT.localPosition = basePos + appliedOffset;
    }
}
