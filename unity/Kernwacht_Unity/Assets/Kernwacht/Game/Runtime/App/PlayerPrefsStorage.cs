using Kernwacht.Sim;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Speicher für <see cref="SaveSystem"/> (Ersatz für window.localStorage des Webspiels).
    /// PlayerPrefs landen im WebGL-Build in der IndexedDB des Browsers; nach jedem Schreiben wird sofort gesichert.
    /// </summary>
    public class PlayerPrefsStorage : ISaveStorage
    {
        public string Load(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public void Save(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void Remove(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        /// <summary>Wie safeStorage() im TS: Probe schreiben/löschen; null, wenn der Speicher nicht nutzbar ist.</summary>
        public static PlayerPrefsStorage safeStorage()
        {
            try
            {
                const string k = "__kw_probe__";
                PlayerPrefs.SetString(k, "1");
                PlayerPrefs.DeleteKey(k);
                return new PlayerPrefsStorage();
            }
            catch
            {
                return null;
            }
        }
    }
}
