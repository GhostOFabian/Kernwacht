using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>
    /// Ansteuerung einer Figur (Gegner, Boss, Held): Laufen/Stehen, Blickrichtung, Angriff, Tod.
    /// Bewegung selbst macht das Spiel – hier wird nur die Darstellung passend geschaltet.
    /// </summary>
    public class UnitView : ModelAnimator
    {
        [Tooltip("Geschwindigkeit (Einheiten/s), bei der der Walk-Clip im Originaltempo läuft.")]
        public float walkSpeedReference = 1f;
        [Tooltip("Drehgeschwindigkeit in Grad pro Sekunde.")]
        public float turnSpeed = 540f;
        [Tooltip("Ab dieser Geschwindigkeit gilt die Figur als laufend.")]
        public float moveThreshold = 0.05f;

        Vector3 _lastPos;
        Vector3 _face;
        bool _auto = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
            _face = transform.forward;
        }

        /// <summary>Automatik aus: dann Laufen/Stehen nur noch über SetMoving.</summary>
        public void SetAutoMovement(bool on) => _auto = on;

        public void SetMoving(bool moving, float speed = 1f)
        {
            SetBase(moving && HasClip(Walk) ? Walk : Idle);
            if (!IsDead && moving) animator.speed = Mathf.Clamp(speed / Mathf.Max(0.01f, walkSpeedReference), 0.3f, 3f);
            else if (!IsDead) animator.speed = 1f;
        }

        /// <summary>Zu einem Punkt schauen (nur Drehung um Y).</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) _face = d.normalized;
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            var delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;
            if (_auto)
            {
                float v = delta.magnitude / dt;
                bool moving = v > moveThreshold;
                SetMoving(moving, v);
                if (moving) _face = delta.normalized;
            }
            if (_face.sqrMagnitude > 0.5f)
            {
                var target = Quaternion.LookRotation(_face, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }
        }
    }
}
