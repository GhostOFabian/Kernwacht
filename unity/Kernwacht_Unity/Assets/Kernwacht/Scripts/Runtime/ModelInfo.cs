using UnityEngine;

namespace Kernwacht.Models
{
    /// <summary>Kennung und Eckdaten am Wurzelobjekt jedes Kernwacht-Prefabs.</summary>
    [DisallowMultipleComponent]
    public class ModelInfo : MonoBehaviour
    {
        public string id;
        public string title;
        public ModelCategory category;
        [Tooltip("Spielfeldgröße in Zellen (Goldgräber = 2).")]
        public int footprint = 1;
    }
}
