using UnityEngine;
using RhythmDojo.UI;
namespace RhythmDojo.EditorTools
{
    public static class SongSelectionLayoutBuilder
    {
        public static SongSelectionView Build()
        {
            var canvas = UiElements.Canvas("Song Selection"); var view = canvas.gameObject.AddComponent<SongSelectionView>();
            UiElements.Label("Title", canvas.transform, "RHYTHM DOJO / SELECT A SONG", new Vector2(50,-30), new Vector2(1000,60), 32);
            string[] labels = { "Song", "Judgment difficulty", "Scroll mode", "Scroll multiplier" };
            var dropdowns = new UnityEngine.UI.Dropdown[4];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = -110 - i * 105;
                UiElements.Label(labels[i]+" Label", canvas.transform, labels[i], new Vector2(50,y), new Vector2(420,32));
                var dropdown = UiElements.Dropdown(labels[i], canvas.transform, new Vector2(50,y-35), new Vector2(420,48));
                dropdowns[i] = dropdown;
            }
            var details = UiElements.Label("Details", canvas.transform, "Select a song", new Vector2(540,-120), new Vector2(680,440));
            var play = UiElements.Button("Play", canvas.transform, "Load Song", new Vector2(50,-590), new Vector2(420,60));
            view.songList = dropdowns[0]; view.difficultyList = dropdowns[1]; view.scrollModeList = dropdowns[2]; view.multiplierList = dropdowns[3]; view.details = details;
            view.play = play;
            view.settingsButton = UiElements.Button("Settings", canvas.transform, "Settings", new Vector2(540,-590), new Vector2(320,60));
            return view;
        }
    }
}
