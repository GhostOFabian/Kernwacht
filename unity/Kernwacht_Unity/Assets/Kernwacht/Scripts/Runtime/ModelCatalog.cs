using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>Kategorie eines Modells (entspricht den Ordnern unter Models/).</summary>
    public enum ModelCategory { Tower, Enemy, Boss, Hero, World }

    /// <summary>
    /// Verzeichnis aller Kernwacht-Modelle: Spiel-Kennung (z. B. "ranger", "runner", "lyra") → Prefab.
    /// Wird vom Menü „Kernwacht → 1 · Modelle einrichten“ erzeugt (Assets/Kernwacht/Data/ModelCatalog.asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Kernwacht/Modellkatalog", fileName = "ModelCatalog")]
    public class ModelCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public string title;
            public ModelCategory category;
            public GameObject prefab;
            public int triangles;
            [TextArea] public string notes;
        }

        public List<Entry> entries = new List<Entry>();

        Dictionary<string, Entry> _index;

        public Entry Find(string id)
        {
            if (_index == null || _index.Count != entries.Count)
            {
                _index = new Dictionary<string, Entry>();
                foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.id)) _index[e.id] = e;
            }
            return id != null && _index.TryGetValue(id, out var r) ? r : null;
        }

        public GameObject Prefab(string id) => Find(id)?.prefab;

        /// <summary>Instanz eines Modells erzeugen (null, wenn die Kennung unbekannt ist).</summary>
        public GameObject Spawn(string id, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var p = Prefab(id);
            if (p == null)
            {
                Debug.LogWarning($"[Kernwacht] Kein Modell für '{id}' im Katalog.");
                return null;
            }
            return Instantiate(p, position, rotation, parent);
        }

        public IEnumerable<Entry> OfCategory(ModelCategory c)
        {
            foreach (var e in entries) if (e.category == c) yield return e;
        }
    }
}
