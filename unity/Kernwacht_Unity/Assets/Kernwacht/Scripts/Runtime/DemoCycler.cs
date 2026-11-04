using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>Nur für die Vorschauszene: schaltet reihum Idle → Walk → Attack → (Death → Neustart).</summary>
    public class DemoCycler : MonoBehaviour
    {
        public float interval = 2.5f;
        public bool includeDeath = true;
        float _t;
        int _step;
        UnitView _unit;
        TowerView _tower;

        void Start()
        {
            _unit = GetComponent<UnitView>();
            _tower = GetComponent<TowerView>();
            if (_unit != null) _unit.SetAutoMovement(false);
            _t = Random.Range(0f, interval);
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < interval) return;
            _t = 0f;
            _step++;
            if (_tower != null)
            {
                if (_step % 2 == 0) _tower.ClearTarget();
                else _tower.SetTarget(transform.position + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * 3f);
                _tower.Fire();
                return;
            }
            if (_unit == null) return;
            switch (_step % (includeDeath ? 4 : 3))
            {
                case 0: _unit.enabled = false; _unit.enabled = true; _unit.SetMoving(false); break; // Neustart nach Death
                case 1: _unit.SetMoving(true); break;
                case 2: _unit.SetMoving(false); _unit.PlayAttack(); break;
                case 3: _unit.PlayDeath(); break;
            }
        }
    }
}
