using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>
    /// Gemeinsame Grundlage für Türme und Figuren: spielt die Clips Idle, Walk, Attack, Death über den Animator
    /// (Zustandsnamen = Clipnamen, vom Editor-Skript angelegt). Attack kehrt automatisch zurück, Death bleibt stehen.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public abstract class ModelAnimator : MonoBehaviour
    {
        public const string Idle = "Idle", Walk = "Walk", Attack = "Attack", Death = "Death";

        [Tooltip("Überblendzeit zwischen Clips in Sekunden.")]
        public float crossFade = 0.12f;

        protected Animator animator;
        readonly HashSet<int> _states = new HashSet<int>();
        string _base = Idle;
        string _oneShot;
        float _oneShotEnd;
        bool _dead;

        public bool IsDead => _dead;

        protected virtual void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
                foreach (var c in animator.runtimeAnimatorController.animationClips)
                    _states.Add(Animator.StringToHash(c.name));
        }

        protected virtual void OnEnable()
        {
            _dead = false;
            _oneShot = null;
            if (animator != null) { animator.speed = 1f; Play(_base, 0f); }
        }

        public bool HasClip(string clip) => _states.Contains(Animator.StringToHash(clip)) || HasState(clip);

        bool HasState(string clip) => animator != null && animator.HasState(0, Animator.StringToHash(clip));

        protected void Play(string clip, float fade)
        {
            if (animator == null || !HasState(clip)) return;
            if (fade <= 0f) animator.Play(clip, 0, 0f);
            else animator.CrossFadeInFixedTime(clip, fade, 0, 0f);
        }

        float ClipLength(string clip)
        {
            if (animator.runtimeAnimatorController == null) return 0.8f;
            foreach (var c in animator.runtimeAnimatorController.animationClips)
                if (c.name == clip) return c.length;
            return 0.8f;
        }

        /// <summary>Dauerclip setzen (Idle oder Walk). Läuft gerade Attack, wird danach dorthin gewechselt.</summary>
        protected void SetBase(string clip)
        {
            if (_dead || _base == clip) return;
            _base = clip;
            if (_oneShot == null) Play(clip, crossFade);
        }

        /// <summary>Angriff abspielen. Liefert die Clipdauer in Sekunden.</summary>
        public float PlayAttack(float speed = 1f)
        {
            if (_dead || !HasState(Attack)) return 0f;
            _oneShot = Attack;
            float len = ClipLength(Attack) / Mathf.Max(0.05f, speed);
            _oneShotEnd = Time.time + len;
            animator.speed = speed;
            Play(Attack, 0.05f);
            return len;
        }

        /// <summary>Todesanimation; die Figur bleibt in der letzten Pose liegen.</summary>
        public float PlayDeath()
        {
            if (_dead) return 0f;
            _dead = true;
            _oneShot = Death;
            float len = HasState(Death) ? ClipLength(Death) : 0f;
            _oneShotEnd = Time.time + len;
            animator.speed = 1f;
            if (len > 0f) Play(Death, 0.08f);
            return len;
        }

        protected virtual void Update()
        {
            if (_oneShot == null) return;
            if (Time.time < _oneShotEnd) return;
            if (_dead)
            {
                animator.speed = 0f; // letzte Pose halten
                _oneShot = null;
                return;
            }
            _oneShot = null;
            animator.speed = 1f;
            Play(_base, crossFade);
        }
    }
}
