using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class DojoEntranceAssets
    {
        public const string Root = "Assets/Game/Art/DojoEntrance/";

        public static Sprite Load(string name)
        {
            // Compatibility for existing callers; new artwork callers specify their crop policy.
            switch (name)
            {
                case "EntranceFrame":
                case "DoorPanel":
                case "TrainingInterior":
                case "TrainingDummy":
                    return Load(name, UiArtworkCrop.Full);
                default:
                    return Load(name, UiArtworkCrop.Alpha);
            }
        }

        public static Sprite Load(string name, UiArtworkCrop crop)
        {
            string path = Root + name + ".asset";
            string texturePath = Root + name + ".png";
            return UiArtworkAssets.Load(texturePath, path, name, crop, Vector4.zero);
        }
    }
}
