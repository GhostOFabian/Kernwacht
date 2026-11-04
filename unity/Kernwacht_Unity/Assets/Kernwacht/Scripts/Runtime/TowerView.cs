using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>
    /// Ansteuerung eines Turms: Knoten „Turret“ dreht sich zum Ziel (um Y), „Muzzle“ ist der Austrittspunkt der Geschosse.
    /// Ohne Ziel läuft die Idle-Animation des Modells (z. B. kreisende Kristalle, schwenkender Ausguck).
    /// </summary>
    public class TowerView : ModelAnimator
    {
        [Tooltip("Wird automatisch gesucht (Knoten „Turret“).")] public Transform turret;
        [Tooltip("Wird automatisch gesucht (Knoten „Muzzle“).")] public Transform muzzle;
        [Tooltip("Drehgeschwindigkeit des Aufsatzes in Grad pro Sekunde.")] public float turnSpeed = 360f;

        Transform _target;
        Vector3? _targetPoint;
        float _yaw;
        bool _aiming;

        protected override void Awake()
        {
            base.Awake();
            if (turret == null) turret = FindDeep(transform, "Turret");
            if (muzzle == null) muzzle = FindDeep(transform, "Muzzle");
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + Vector3.up;

        public void SetTarget(Transform target) { _target = target; _targetPoint = null; }
        public void SetTarget(Vector3 worldPoint) { _target = null; _targetPoint = worldPoint; }
        public void ClearTarget() { _target = null; _targetPoint = null; }

        /// <summary>Schuss: Attack-Clip (Rückstoß) abspielen und den Austrittspunkt liefern.</summary>
        public Vector3 Fire(float speed = 1f)
        {
            PlayAttack(speed);
            return MuzzlePosition;
        }

        /// <summary>Ist der Aufsatz (fast) auf das Ziel ausgerichtet?</summary>
        public bool IsAimed(float toleranceDeg = 8f)
        {
            if (turret == null || (!_targetPoint.HasValue && _target == null)) return true;
            Vector3 p = _target != null ? _target.position : _targetPoint.Value;
            var d = p - turret.position; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return true;
            var f = turret.forward; f.y = 0f;
            return Vector3.Angle(f, d) <= toleranceDeg;
        }

        void LateUpdate()
        {
            if (turret == null) return;
            Vector3? p = _target != null ? (Vector3?)_target.position : _targetPoint;
            if (!p.HasValue) { _aiming = false; return; }
            var parent = turret.parent != null ? turret.parent : transform;
            var local = parent.InverseTransformPoint(p.Value) - turret.localPosition;
            float want = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (!_aiming) { _yaw = turret.localEulerAngles.y; _aiming = true; }
            _yaw = Mathf.MoveTowardsAngle(_yaw, want, turnSpeed * Time.deltaTime);
            // nach dem Animator anwenden, damit die Zielausrichtung gewinnt
            turret.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}
