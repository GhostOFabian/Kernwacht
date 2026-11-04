using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Isometrische, orthografische Kamera – Übertragung von render/Camera.ts (IsoCamera) des Webspiels.
    /// Spielkoordinaten wie im Webspiel: x = Spalte, z = Zeile (Zellmitte = +0,5), y = oben (rechtshändig).
    /// Unity ist linkshändig: Spiel-(x, y, z) liegt in Unity bei (x, y, −z) – <see cref="W"/>. Dadurch ist das Bild
    /// nicht gespiegelt und entspricht exakt dem Webspiel.
    /// Isometrie 35,26°, Draufsicht 62° (Taste V), Vierteldrehungen (Q/E), freie Drehung und Neigung,
    /// Zoom zum Mauszeiger, Schwenken, Kamerafahrten (Bossauftritt) und Heldenverfolgung.
    /// </summary>
    public class IsoCamera
    {
        static readonly float PITCH = Mathf.Atan(1f / Mathf.Sqrt(2f));
        static readonly float PITCH_TOP = 62f * Mathf.Deg2Rad;
        static readonly float PITCH_MIN = 17f * Mathf.Deg2Rad;
        static readonly float PITCH_MAX = 80f * Mathf.Deg2Rad;

        public float tx, tz;
        public float zoom = 8, minZoom = 3, maxZoom = 16;
        public float rot;                 // Ziel-Rotation in Vierteldrehungen
        public float pitch = PITCH;
        public bool topDown;
        public float freeYaw, pitchAdj;
        float yawAnim = Mathf.PI / 4;
        public float width = 1, height = 1;
        public float mapW = 20, mapH = 14;
        public float spin;
        float spinYaw;
        float focusT, focusDur;
        Vector3 focusFrom, focusTo;
        public bool lockRotation;

        public readonly Camera cam;

        public IsoCamera(Camera cam)
        {
            this.cam = cam;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
        }

        public float Yaw => yawAnim + spinYaw + freeYaw;

        /// <summary>Spielkoordinaten → Unity-Weltposition.</summary>
        public static Vector3 W(float x, float y, float z) => new Vector3(x, y, -z);
        /// <summary>Unity-Weltposition → Spielkoordinaten.</summary>
        public static Vector3 G(Vector3 u) => new Vector3(u.x, u.y, -u.z);

        /// <summary>Freies Drehen: Gier und Neigung (Bogenmaß), Neigung begrenzt auf ca. 17°–80°.</summary>
        public void Orbit(float dYaw, float dPitch)
        {
            freeYaw += dYaw;
            float b = topDown ? PITCH_TOP : PITCH;
            pitchAdj = Mathf.Max(PITCH_MIN - b, Mathf.Min(PITCH_MAX - b, pitchAdj + dPitch));
        }

        public void ResetView()
        {
            rot = Mathf.Floor((yawAnim - Mathf.PI / 4) / (Mathf.PI / 2) + 0.5f);
            freeYaw = 0;
            pitchAdj = 0;
        }

        public int YawDeg
        {
            get
            {
                float d = (((Yaw * 180f / Mathf.PI) % 360f) + 360f) % 360f;
                return Mathf.FloorToInt(d + 0.5f);
            }
        }

        public void SetMap(float w, float h)
        {
            mapW = w; mapH = h;
            tx = w / 2; tz = h / 2;
            Fit();
        }

        /// <summary>Zoom so wählen, dass die ganze Karte sichtbar ist.</summary>
        public void Fit()
        {
            float aspect = width / Mathf.Max(1, height);
            float diag = (mapW + mapH) / Mathf.Sqrt(2f);
            float horiz = diag / 2;
            float vert = diag * Mathf.Sin(pitch) / 2 + 1.2f;
            maxZoom = Mathf.Max(horiz / aspect, vert) * 1.25f;
            zoom = Mathf.Max(horiz / aspect, vert) * 1.04f;
            minZoom = 2.6f;
            Clamp();
        }

        public void Resize(float w, float h)
        {
            bool changed = w != width || h != height;
            width = Mathf.Max(1, w);
            height = Mathf.Max(1, h);
            if (changed) Fit();
        }

        /// <summary>Fährt für dur Sekunden zu (x, z) mit Zoomfaktor und kehrt dann zurück.</summary>
        public void Focus(float x, float z, float zoomMul, float dur)
        {
            focusFrom = new Vector3(tx, tz, zoom);
            focusTo = new Vector3(x, z, Mathf.Max(minZoom, zoom * zoomMul));
            focusT = dur;
            focusDur = dur;
        }

        public bool Focusing => focusT > 0;

        public bool ToggleTopDown()
        {
            topDown = !topDown;
            return topDown;
        }

        public void Rotate(int dir)
        {
            if (lockRotation) return;
            rot += dir;
        }

        public void Update(float dt)
        {
            float wantPitch = Mathf.Max(PITCH_MIN, Mathf.Min(PITCH_MAX, (topDown ? PITCH_TOP : PITCH) + pitchAdj));
            pitch += (wantPitch - pitch) * Mathf.Min(1, dt * 6);
            float target = Mathf.PI / 4 + rot * (Mathf.PI / 2);
            float diff = target - yawAnim;
            yawAnim += Mathf.Abs(diff) < 0.001f ? diff : diff * Mathf.Min(1, dt * 10);
            spinYaw += spin * dt;
            if (focusT > 0)
            {
                focusT = Mathf.Max(0, focusT - dt);
                float p = 1 - focusT / focusDur;
                float k = p < 0.25f ? Ease(p / 0.25f) : p < 0.8f ? 1 : 1 - Ease((p - 0.8f) / 0.2f);
                tx = focusFrom.x + (focusTo.x - focusFrom.x) * k;
                tz = focusFrom.y + (focusTo.y - focusFrom.y) * k;
                zoom = focusFrom.z + (focusTo.z - focusFrom.z) * k;
            }
            Apply();
        }

        /// <summary>Kamera-Transform setzen. Blickrichtung wie im Webspiel: von (tx,0,tz) + toCam·60 auf (tx,0,tz).</summary>
        public void Apply()
        {
            float yaw = Yaw;
            float dx = Mathf.Cos(pitch) * Mathf.Sin(yaw);
            float dy = Mathf.Sin(pitch);
            float dz = Mathf.Cos(pitch) * Mathf.Cos(yaw);
            const float d = 60f;
            var target = W(tx, 0, tz);
            cam.transform.position = target + new Vector3(dx, dy, -dz) * d;
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            cam.orthographicSize = zoom;
        }

        /// <summary>Schwenken um Bildschirmpixel (Inhalt folgt dem Zeiger). dy in Bildschirmkoordinaten nach unten positiv.</summary>
        public void Pan(float dxPix, float dyPix)
        {
            float s = 2 * zoom / height;
            float yaw = Yaw;
            float rx = Mathf.Cos(yaw), rz = -Mathf.Sin(yaw);
            float fx = -Mathf.Sin(yaw), fz = -Mathf.Cos(yaw);
            float k = s / Mathf.Sin(pitch);
            // Inhalt folgt dem Zeiger: Kamera bewegt sich entgegengesetzt (wie Camera.ts pan)
            tx += -dxPix * s * rx + dyPix * k * fx;
            tz += -dxPix * s * rz + dyPix * k * fz;
            Clamp();
            Apply();
        }

        /// <summary>Zoom um Faktor (>1 = heraus), optional zum Bildschirmpunkt (Pixel, Ursprung oben links).</summary>
        public void ZoomBy(float factor, Vector2? screenTopLeft = null)
        {
            Vector3? before = screenTopLeft.HasValue ? ScreenToGround(screenTopLeft.Value, 0) : null;
            zoom = Mathf.Min(maxZoom, Mathf.Max(minZoom, zoom * factor));
            Apply();
            if (before.HasValue && screenTopLeft.HasValue)
            {
                var after = ScreenToGround(screenTopLeft.Value, 0);
                if (after.HasValue)
                {
                    tx += before.Value.x - after.Value.x;
                    tz += before.Value.z - after.Value.z;
                }
            }
            Clamp();
            Apply();
        }

        public void Clamp()
        {
            const float m = 1;
            tx = Mathf.Min(mapW + m, Mathf.Max(-m, tx));
            tz = Mathf.Min(mapH + m, Mathf.Max(-m, tz));
        }

        /// <summary>Bildschirmpunkt (Pixel, Ursprung oben links wie im Webspiel) → Schnitt mit Ebene y = planeY.</summary>
        public Vector3? ScreenToGround(Vector2 topLeft, float planeY)
        {
            var ray = cam.ScreenPointToRay(new Vector3(topLeft.x, Screen.height - topLeft.y, 0));
            var plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            if (!plane.Raycast(ray, out float t)) return null;
            return G(ray.GetPoint(t));
        }

        /// <summary>Spielpunkt → Bildschirm (Pixel, Ursprung oben links).</summary>
        public Vector2 WorldToScreen(Vector3 game)
        {
            var s = cam.WorldToScreenPoint(W(game.x, game.y, game.z));
            return new Vector2(s.x, Screen.height - s.y);
        }

        static float Ease(float t)
        {
            float c = Mathf.Clamp01(t);
            return c * c * (3 - 2 * c);
        }
    }
}
