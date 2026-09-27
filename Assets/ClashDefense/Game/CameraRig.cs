using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Cámara fija y ortográfica (Doc 03 §20, LDS-001.1): encuadra todo el contenido dentro del área útil,
    /// libre de las franjas del HUD de UXS-001.6 (superior 7 %, inferior 14 %, laterales 5 %). Se recalcula si cambia la resolución.
    /// </summary>
    public sealed class CameraRig
    {
        public const float TopBand = 0.07f, BottomBand = 0.14f, SideBand = 0.05f;
        const float Pitch = 55f;

        readonly Camera cam;
        Bounds content;
        int lastW, lastH;

        public Camera Camera => cam;

        public CameraRig(Camera camera, Bounds contentBounds)
        {
            cam = camera;
            content = contentBounds;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.FueraDelArea * 0.8f;
            Fit();
        }

        /// <summary>Otro nivel, otro encuadre (cada mapa del Mundo 1 se ve completo, Doc 03 §20).</summary>
        public void SetContent(Bounds contentBounds)
        {
            content = contentBounds;
            Fit();
        }

        public void Tick()
        {
            if (Screen.width != lastW || Screen.height != lastH) Fit();
        }

        public void Fit()
        {
            lastW = Screen.width; lastH = Screen.height;
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            var inv = Quaternion.Inverse(rot);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? content.min.x : content.max.x,
                    (i & 2) == 0 ? content.min.y : content.max.y,
                    (i & 4) == 0 ? content.min.z : content.max.z);
                var l = inv * c;
                min = Vector3.Min(min, l); max = Vector3.Max(max, l);
            }
            float w = max.x - min.x, h = max.y - min.y;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float usableW = 1f - 2f * SideBand, usableH = 1f - TopBand - BottomBand;
            float size = Mathf.Max(h / (2f * usableH), w / (2f * aspect * usableW)) * 1.02f;
            cam.orthographicSize = size;
            float cx = (min.x + max.x) * 0.5f, cy = (min.y + max.y) * 0.5f;
            float targetViewportY = BottomBand + usableH * 0.5f;
            float offsetY = (targetViewportY - 0.5f) * 2f * size;
            var local = new Vector3(cx, cy - offsetY, min.z - 30f);
            cam.transform.rotation = rot;
            cam.transform.position = rot * local;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = (max.z - min.z) + 80f;
        }

        /// <summary>Punto del plano de juego (y = 0) bajo una posición de pantalla.</summary>
        public bool ScreenToGround(Vector3 screen, out Vector3 world)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { world = ray.GetPoint(d); return true; }
            world = Vector3.zero;
            return false;
        }
    }
}
