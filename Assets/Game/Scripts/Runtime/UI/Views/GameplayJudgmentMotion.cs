using System;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class GameplayJudgmentMotion : MonoBehaviour
    {
        public CanvasGroup group;
        public Text label;
        public Text echo;
        public DojoInkGraphic stroke;

        private Judgment grade;
        private RectTransform rect;
        private float emphasis = 1;

        public void Validate()
        {
            if (!group || !label)
            {
                throw new InvalidOperationException("Gameplay judgment motion references missing.");
            }
            if (!echo || !stroke)
            {
                throw new InvalidOperationException("Gameplay judgment brush references missing.");
            }
            rect = (RectTransform)group.transform;
        }

        public void Play(Judgment grade, bool hold)
        {
            this.grade = grade;
            emphasis = 1;
            if (hold)
            {
                emphasis = .85f;
            }
            echo.text = label.text;
            echo.fontSize = label.fontSize;
            stroke.color = label.color;
            Render(0);
        }

        public void Render(float age)
        {
            float duration = .46f;
            float size = 1;
            float travel = 5;
            float angle = -2;
            float echoOpacity = .18f;
            switch (grade)
            {
                case Judgment.Perfect:
                    size = 1.08f;
                    break;
                case Judgment.Good:
                    duration = .40f;
                    size = .96f;
                    travel = 3;
                    angle = 1;
                    echoOpacity = .08f;
                    break;
                case Judgment.Miss:
                    duration = .42f;
                    size = .91f;
                    travel = -7;
                    angle = -1;
                    echoOpacity = 0;
                    break;
            }

            float enter = Mathf.Clamp01(age / .12f);
            float back = enter - 1;
            float settle = 1 + 2.2f * back * back * back + 1.2f * back * back;
            float exit = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.19f, duration, age));
            group.alpha = .84f * (1 - exit);
            rect.localScale = Vector3.one * (size * emphasis * Mathf.Lerp(.86f, 1, settle));
            rect.anchoredPosition = new Vector2((1 - settle) * -5, exit * travel);
            rect.localRotation = Quaternion.Euler(0, 0, angle * (1 - enter));

            var tint = label.color;
            tint.a = echoOpacity * (1 - Mathf.Clamp01(age / .18f));
            echo.color = tint;
            echo.rectTransform.anchoredPosition = new Vector2(-3 - enter * 5, -2);
            echo.rectTransform.localScale = Vector3.one * (1.02f + enter * .03f);
            stroke.rectTransform.localScale = new Vector3(.65f + .35f * settle, 1, 1);
            stroke.canvasRenderer.SetAlpha((1 - exit) * .65f);
        }

        public void ResetPresentation()
        {
            Validate();
            group.alpha = 0;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            echo.text = "";
        }
    }
}
