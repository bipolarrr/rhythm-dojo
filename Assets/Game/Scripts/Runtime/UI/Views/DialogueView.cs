using System;
using RhythmDojo.Dialogue;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class DialogueView : MonoBehaviour
    {
        public GameObject root;
        public CanvasGroup group;
        public RectTransform paper;
        public RectTransform actor;
        public Image portrait;
        public Image previousPortrait;
        public Sprite[] expressions;
        public Text speaker;
        public Text body;
        public Text counter;
        public Text hint;
        public Button advance;
        public Button skip;
        public AudioSource source;
        public AudioClip turnSound;
        public AudioClip arrivalSound;

        private DialogueMotion motion;
        private float lineAge;

        public void Validate()
        {
            if (!root || !group || !paper)
            {
                throw new InvalidOperationException("Dialogue stage missing. Rebuild the scene.");
            }
            if (!actor || !portrait || !previousPortrait)
            {
                throw new InvalidOperationException("Dialogue actor missing.");
            }
            if (!speaker || !body || !counter)
            {
                throw new InvalidOperationException("Dialogue text missing.");
            }
            if (!hint || !advance || !skip)
            {
                throw new InvalidOperationException("Dialogue controls missing.");
            }
            if (!source || !turnSound || !arrivalSound)
            {
                throw new InvalidOperationException("Dialogue audio missing.");
            }
            if (expressions == null || expressions.Length != Enum.GetValues(typeof(DialogueExpression)).Length)
            {
                throw new InvalidOperationException("Dialogue expressions missing.");
            }
            foreach (var expression in expressions)
            {
                if (!expression)
                {
                    throw new InvalidOperationException("Dialogue portrait reference missing.");
                }
            }
        }

        public void ShowLine(DialogueLine line, int index, int count)
        {
            previousPortrait.sprite = portrait.sprite;
            portrait.sprite = expressions[(int)line.Expression];
            if (index == 0)
            {
                previousPortrait.sprite = portrait.sprite;
            }
            speaker.text = line.Speaker;
            counter.text = $"{index + 1:00}  /  {count:00}";
            body.text = "";
            motion = line.Motion;
            lineAge = 0;
        }

        public void Animate(float visibility, float deltaTime)
        {
            lineAge += deltaTime;
            float remaining = 1 - Mathf.Clamp01(visibility);
            float eased = 1 - remaining * remaining * remaining;
            group.alpha = eased;
            paper.anchoredPosition = new Vector2(0, -32 * (1 - eased));
            float nod = 0;
            float lean = 0;
            switch (motion)
            {
                case DialogueMotion.Nod:
                    nod = Mathf.Sin(Mathf.Clamp01(lineAge / .65f) * Mathf.PI) * -7;
                    break;
                case DialogueMotion.Lean:
                    lean = Mathf.Sin(Mathf.Clamp01(lineAge / 1.2f) * Mathf.PI) * 5;
                    break;
            }
            actor.anchoredPosition = new Vector2(36 * (1 - eased) - lean,
                Mathf.Sin(lineAge * 1.6f) * 1.8f + nod);
            actor.localRotation = Quaternion.Euler(0, 0, lean * .08f);
            float blend = Mathf.Clamp01(lineAge / .16f);
            portrait.color = new Color(1, 1, 1, blend);
            previousPortrait.color = new Color(1, 1, 1, 1 - blend);
        }
    }
}
