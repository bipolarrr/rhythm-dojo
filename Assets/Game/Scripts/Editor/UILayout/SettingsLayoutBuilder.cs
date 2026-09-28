using UnityEngine;
using RhythmDojo.UI;
namespace RhythmDojo.EditorTools
{
    public static class SettingsLayoutBuilder
    {
        public static SettingsView Build()
        {
            var canvas = UiElements.Canvas("Settings"); var view = canvas.gameObject.AddComponent<SettingsView>();
            UiElements.Label("Title", canvas.transform, "RHYTHM DOJO / AUDIO SETTINGS", new Vector2(50,-30), new Vector2(1100,60), 32);
            UiElements.Label("Buffer Label", canvas.transform, "Unity DSP buffer size", new Vector2(50,-130), new Vector2(420,32));

            view.bufferList = UiElements.Dropdown("DSP Buffer", canvas.transform, new Vector2(50,-175), new Vector2(420,48));
            view.actualState = UiElements.Label("Actual Audio State", canvas.transform, "", new Vector2(540,-130), new Vector2(650,140));
            view.status = UiElements.Label("Audio Status", canvas.transform, "", new Vector2(50,-300), new Vector2(1150,120));
            UiElements.Label("Help", canvas.transform, "This changes Unity's mixer buffer; it does not select ASIO or set its driver buffer.\nSmaller buffers may cause audio dropouts; Unity may choose a supported size.\nDefault restores the actual buffer size at app startup.\nThis selection lasts only while the app is running.", new Vector2(50,-445), new Vector2(1150,130));
            view.apply = UiElements.Button("Apply", canvas.transform, "Apply", new Vector2(50,-610), new Vector2(420,60));
            view.back = UiElements.Button("Back", canvas.transform, "Back to Song List", new Vector2(540,-610), new Vector2(420,60));
            return view;
        }
    }
}
