using Kernwacht.Models;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Verweise, die das Spiel zur Laufzeit braucht (Modellkatalog und Grundmaterialien).
    /// Wird von „Kernwacht → 3 · Spielszene erzeugen“ unter Assets/Kernwacht/Resources/KernwachtGameAssets.asset angelegt,
    /// damit alle Prefabs, Shader und Materialien sicher im WebGL-Build landen.
    /// </summary>
    [CreateAssetMenu(menuName = "Kernwacht/Spiel-Assets", fileName = "KernwachtGameAssets")]
    public class GameAssets : ScriptableObject
    {
        public ModelCatalog catalog;
        [Tooltip("Beleuchtetes Material (URP Lit) für Gelände und Ersatzformen.")] public Material lit;
        [Tooltip("Unbeleuchtetes Material für Geschosse und Strahlen.")] public Material unlit;
        [Tooltip("Durchscheinendes, unbeleuchtetes Material für Reichweitenkreise und Explosionen.")] public Material transparent;

        static GameAssets _inst;
        public static GameAssets Instance
        {
            get
            {
                if (_inst == null) _inst = Resources.Load<GameAssets>("KernwachtGameAssets");
                return _inst;
            }
        }
    }
}
