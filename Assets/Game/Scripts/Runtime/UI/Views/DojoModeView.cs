using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Fixed stage composition is authored by AlphaDojoLayoutBuilder. Only poses change here.
    public sealed class DojoModeView : MonoBehaviour
    {
        public CanvasGroup[] stages;
        public RectTransform[] props;
        public RectTransform[] companions;
        public RectTransform[] banners;
        public Image character;
        public Sprite[] poses;
        public Text heading, description, detail, counter, actionLabel;
        public Button previous, next;
        public AudioSource effects;
        public AudioClip contact;
        public int SelectedIndex { get; private set; } = 1;
        public bool IsMoving { get; private set; }
        public bool InputEnabled { get; set; }
        public bool CanEnter => SelectedIndex != 3;
        public int LessonHits { get; private set; }
        public int ContactCount { get; private set; }
        private float age;
        private float reaction;
        private bool struck;
        private bool lesson;
        private int lessonBeat = -1;
        private int cueBeat = -1;
        private Vector2 characterHome;
        private Vector2[] propHomes;
        private Vector2[] companionHomes;
        private readonly string[] titles = { "비급 전수", "자유 연습", "실력 검정", "새로운 수련" };
        private readonly string[] descriptions = {
            "리듬게임이 처음인가? 들어오도록!", "자유롭게 연습하자!",
            "하산할 준비가 되었다고 생각하나?", "다음 수련은 아직 준비 중입니다." };
        private readonly string[] details = {
            "입문 수련 · 사부의 박자에 맞춰 네 번 두드리기",
            "자유 수련 · 곡과 난이도를 골라 도전하세요",
            "Challenge · 기존 제한 조건과 판정 규칙으로 실력 증명",
            "네 번째 모드 · 준비 중" };

        public void Validate()
        {
            if (!character || poses == null || poses.Length != 4)
            {
                throw new InvalidOperationException("Dojo trainee poses missing.");
            }
            if (stages.Length != 4 || props.Length != 4 || companions.Length != 4)
            {
                throw new InvalidOperationException("Dojo stage composition missing.");
            }
            if (!heading || !description || !detail)
            {
                throw new InvalidOperationException("Dojo mode labels missing.");
            }
            if (!previous || !next || !actionLabel)
            {
                throw new InvalidOperationException("Dojo mode controls missing.");
            }
        }

        public void ResetPresentation()
        {
            Validate();
            StopAllCoroutines();
            if (propHomes == null)
            {
                characterHome = character.rectTransform.anchoredPosition;
                propHomes = new Vector2[4];
                companionHomes = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    propHomes[i] = props[i].anchoredPosition;
                    companionHomes[i] = companions[i].anchoredPosition;
                }
            }
            IsMoving = false;
            lesson = false;
            LessonHits = 0;
            SelectedIndex = 1;
            character.rectTransform.anchoredPosition = characterHome;
            character.rectTransform.localScale = Vector3.one;
            ShowStage();
        }

        public void Move(int direction)
        {
            if (!InputEnabled || IsMoving)
            {
                return;
            }
            StartCoroutine(Travel(Math.Sign(direction)));
        }

        private IEnumerator Travel(int direction)
        {
            IsMoving = true;
            lesson = false;
            float elapsed = 0f;
            int previousIndex = SelectedIndex;
            SelectedIndex = (SelectedIndex + direction + 4) % 4;
            character.rectTransform.localScale = new Vector3(direction, 1, 1);
            while (elapsed < .6f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .6f);
                float blend = Mathf.SmoothStep(0, 1, t);
                stages[previousIndex].alpha = 1 - blend;
                stages[SelectedIndex].alpha = blend;
                ((RectTransform)stages[previousIndex].transform).anchoredPosition = new Vector2(-direction * 90 * blend, 0);
                ((RectTransform)stages[SelectedIndex].transform).anchoredPosition = new Vector2(direction * 90 * (1 - blend), 0);
                character.sprite = poses[1 + ((int)(elapsed * 12) % 2)];
                character.rectTransform.anchoredPosition = characterHome + new Vector2(direction * Mathf.Sin(t * Mathf.PI * 2) * 100, Mathf.Abs(Mathf.Sin(elapsed * 30)) * 7);
                float textAlpha = Mathf.Abs(t * 2 - 1);
                description.color = new Color(1, .9f, .72f, textAlpha);
                heading.color = new Color(1, .89f, .68f, textAlpha);
                detail.color = new Color(1, .89f, .68f, textAlpha);
                if (t >= .5f)
                {
                    UpdateLabels();
                }
                yield return null;
            }
            character.rectTransform.localScale = Vector3.one;
            character.rectTransform.anchoredPosition = characterHome;
            ShowStage();
            IsMoving = false;
        }

        private void ShowStage()
        {
            age = reaction = 0;
            struck = false;
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i].alpha = 0;
                if (i == SelectedIndex)
                {
                    stages[i].alpha = 1;
                }
                ((RectTransform)stages[i].transform).anchoredPosition = Vector2.zero;
                stages[i].blocksRaycasts = false;
                props[i].localRotation = Quaternion.identity;
                props[i].anchoredPosition = propHomes[i];
            }
            character.sprite = poses[0];
            description.color = new Color(1, .9f, .72f);
            heading.color = detail.color = new Color(1, .89f, .68f);
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            heading.text = titles[SelectedIndex];
            description.text = descriptions[SelectedIndex];
            detail.text = details[SelectedIndex];
            counter.text = $"0{SelectedIndex + 1} / 04";
            actionLabel.text = "수련 시작";
            if (SelectedIndex == 0)
            {
                actionLabel.text = "박자 배우기";
            }
            else if (SelectedIndex == 3)
            {
                actionLabel.text = "준비 중";
            }
        }

        public void Practice()
        {
            if (!lesson)
            {
                lesson = true;
                age = 0;
                LessonHits = 0;
                lessonBeat = -1;
                cueBeat = -1;
                actionLabel.text = "두드리기";
                detail.text = "사부가 움직이는 박자에 Space 또는 버튼!  0 / 4";
                return;
            }
            int beat = Mathf.FloorToInt(age / .75f);
            float phase = age % .75f;
            if (beat == lessonBeat)
            {
                return;
            }
            lessonBeat = beat;
            Strike();
            if (phase < .19f || phase > .56f)
            {
                LessonHits++;
                detail.text = $"좋다! 박자를 느껴 보거라.  {LessonHits} / 4";
            }
            else
            {
                detail.text = "조금 천천히! 사부가 손짓할 때 두드려 보거라.";
            }
            if (LessonHits >= 4)
            {
                lesson = false;
                detail.text = "기초 수련 완료! → 자유 연습에서 곡을 골라 보세요.";
                actionLabel.text = "다시 배우기";
            }
        }

        private void Strike()
        {
            reaction = .45f;
            ContactCount++;
            if (SelectedIndex < 2 && effects && contact)
            {
                effects.PlayOneShot(contact, .22f);
            }
        }

        private void Update()
        {
            if (!InputEnabled || IsMoving || propHomes == null)
            {
                return;
            }
            age += Time.unscaledDeltaTime;
            reaction = Mathf.Max(0, reaction - Time.unscaledDeltaTime);
            float cycle = age % 3f;
            if (cycle < 1.3f)
            {
                struck = false;
            }
            if (!lesson && cycle >= 1.3f && !struck)
            {
                struck = true;
                Strike();
            }
            character.sprite = poses[0];
            character.rectTransform.localRotation = Quaternion.identity;
            character.rectTransform.anchoredPosition = characterHome + Vector2.up * (Mathf.Sin(age * 4) * 2);
            if (reaction > .25f)
            {
                character.sprite = poses[3];
                if (SelectedIndex == 2)
                {
                    character.sprite = poses[0];
                    character.rectTransform.localRotation = Quaternion.Euler(0, 0, -reaction * 9);
                }
                character.rectTransform.anchoredPosition = characterHome + Vector2.right * 35;
            }
            props[SelectedIndex].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(reaction * 38) * reaction * 22);
            companions[SelectedIndex].anchoredPosition = companionHomes[SelectedIndex] + Vector2.up * (Mathf.Sin(age * 2) * 2);
            companions[SelectedIndex].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 3) * 1.5f);
            if (lesson)
            {
                companions[0].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age / .75f * Mathf.PI * 2) * 5);
                int beat = Mathf.FloorToInt(age / .75f);
                if (beat != cueBeat)
                {
                    cueBeat = beat;
                    if (effects && contact)
                    {
                        effects.PlayOneShot(contact, .32f);
                    }
                }
            }
            banners[SelectedIndex].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 1.7f) * 3);
        }
    }
}
