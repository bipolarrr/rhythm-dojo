using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class SettingsArtAssets
    {
        public static Sprite Load(string name)
        {
            return Load(name, UiArtworkCrop.Full, new Vector4(128, 128, 128, 128));
        }

        public static Sprite Load(string name, UiArtworkCrop crop, Vector4 border)
        {
            string root = DojoEntranceAssets.Root;
            string path = root + name + ".png";
            return UiArtworkAssets.Load(path, root + name + ".asset", name, crop, border);
        }
    }
}
