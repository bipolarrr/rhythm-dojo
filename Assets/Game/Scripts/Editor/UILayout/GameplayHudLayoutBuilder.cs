using UnityEngine;
using RhythmDojo.UI;
namespace RhythmDojo.EditorTools
{
    public static class GameplayHudLayoutBuilder
    {
        public static GameplayHudView Build()
        {
            var canvas = UiElements.Canvas("HUD"); var view = canvas.gameObject.AddComponent<GameplayHudView>();
            var status = UiElements.Label("Session Status", canvas.transform, "RHYTHM DOJO\nSPACE to start", new Vector2(28,-25), new Vector2(900,330));
            view.status = status;
            var laneRoot = new GameObject("Lane Feedback", typeof(RectTransform)).GetComponent<RectTransform>();
            laneRoot.SetParent(canvas.transform, false); laneRoot.anchorMin = Vector2.zero; laneRoot.anchorMax = Vector2.one;
            laneRoot.offsetMin = laneRoot.offsetMax = Vector2.zero;
            view.lanes = canvas.gameObject.AddComponent<HudLaneLayout>(); view.lanes.laneRoot = laneRoot;
            view.returnButton = UiElements.Button("Song List", canvas.transform, "Song List (ESC)", new Vector2(1000,-30), new Vector2(240,48));
            return view;
        }
    }
}
