using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Services
{
    // A play request owns the lock through loading, pause, restart and return to a menu.
    // The scene check also covers direct scene entry and additive legacy Settings loads.
    public static class SettingsAccessPolicy
    {
        private static readonly HashSet<object> owners = new HashSet<object>();

        public static bool CanChange
        {
            get
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    return true;
                }
                return owners.Count == 0
                    && !SceneManager.GetSceneByPath("Assets/Game/Scenes/Gameplay.unity").isLoaded;
            }
        }

        internal static void Block(object owner) => owners.Add(owner);

        internal static void Release(object owner) => owners.Remove(owner);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => owners.Clear();
    }
}
