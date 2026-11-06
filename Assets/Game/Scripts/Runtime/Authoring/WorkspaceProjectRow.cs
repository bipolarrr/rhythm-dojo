using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class WorkspaceProjectRow : MonoBehaviour, IPointerClickHandler
    {
        public Text projectLabel;
        public Text savedAtLabel;
        public Button recentToggle;
        public Button copyProjectId;

        private Action open;

        public void Initialize(string label, bool showInRecent, Action openProject, Func<bool> toggleRecent)
        {
            projectLabel.text = label;
            open = openProject;
            var star = recentToggle.GetComponentInChildren<Text>(true);
            if (!star.font.HasCharacter('★') || !star.font.HasCharacter('☆'))
            {
                star.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                star.fontStyle = FontStyle.Normal;
            }

            SetStar(star, showInRecent);
            recentToggle.onClick.AddListener(() => SetStar(star, toggleRecent()));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            {
                open?.Invoke();
            }
        }

        private static void SetStar(Text star, bool showInRecent)
        {
            if (showInRecent)
            {
                star.text = "★";
                star.color = new Color(.52f, .33f, .03f);
                return;
            }

            star.text = "☆";
            star.color = new Color(.32f, .32f, .32f);
        }
    }
}
