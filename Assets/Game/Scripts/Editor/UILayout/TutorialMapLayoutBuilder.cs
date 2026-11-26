using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    internal static class TutorialMapLayoutBuilder
    {
        private const string Art = "Assets/Game/Art/AlphaDojo/Training/";
        private static readonly Vector2 MapSize = new Vector2(1056, 594);
        private static readonly string[] Ids =
        {
            "trill", "stairs-denim", "jack", "burst", "hold-tap", "shield-release", "chord-trill",
        };

        // Paper coordinates from the upper left. Paths and tablet centers use the same points.
        private static readonly Vector2[] Centers =
        {
            new Vector2(125, 428), new Vector2(292, 314), new Vector2(478, 235),
            new Vector2(696, 260), new Vector2(370, 437), new Vector2(621, 385), new Vector2(910, 291),
        };

        public static void Build(TutorialView view, int lessonCount)
        {
            if (lessonCount != Ids.Length)
            {
                throw new InvalidOperationException("The tutorial map requires one lesson per tablet.");
            }

            var map = new GameObject("Lesson Map", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            map.rectTransform.SetParent(view.storyRoot, false);
            map.rectTransform.anchorMin = map.rectTransform.anchorMax = new Vector2(.5f, .45f);
            map.rectTransform.sizeDelta = MapSize;
            map.sprite = SceneResources.Require<Sprite>(Art + "HanjiMap.png");
            map.raycastTarget = false;
            view.lessonMap = map.gameObject;

            var choices = map.rectTransform;
            Path(choices, 0, 1, new Vector2(179, 398), new Vector2(221, 330));
            Path(choices, 1, 2, new Vector2(369, 304), new Vector2(405, 238));
            Path(choices, 2, 3, new Vector2(563, 228), new Vector2(604, 266));
            Path(choices, 3, 6, new Vector2(779, 255), new Vector2(837, 292));
            Path(choices, 0, 4, new Vector2(213, 449), new Vector2(282, 451));
            Path(choices, 4, 5, new Vector2(474, 441), new Vector2(529, 417));
            Path(choices, 5, 6, new Vector2(727, 373), new Vector2(820, 307));

            view.lessonIds = (string[])Ids.Clone();
            view.lessonButtons = new Button[lessonCount];
            view.lessonLabels = new Text[lessonCount];
            view.completionSeals = new GameObject[lessonCount];
            var stone = SceneResources.Require<Sprite>(Art + "StoneTablet.png");
            for (int i = 0; i < lessonCount; i++)
            {
                Tablet(view, choices, stone, i);
            }

            var buttons = view.lessonButtons;
            Navigate(buttons[0], view.backButton, buttons[1], buttons[1], buttons[4]);
            Navigate(buttons[1], buttons[0], buttons[2], view.backButton, buttons[0]);
            Navigate(buttons[2], buttons[1], buttons[3], view.backButton, buttons[4]);
            Navigate(buttons[3], buttons[2], buttons[6], view.backButton, buttons[5]);
            Navigate(buttons[4], buttons[0], buttons[5], buttons[1], null);
            Navigate(buttons[5], buttons[4], buttons[6], buttons[3], null);
            Navigate(buttons[6], buttons[3], null, buttons[3], buttons[5]);
        }

        private static void Path(RectTransform map, int from, int to, Vector2 firstTangent, Vector2 secondTangent)
        {
            var path = new GameObject(Ids[from] + " to " + Ids[to], typeof(RectTransform)).GetComponent<RectTransform>();
            path.SetParent(map, false);
            path.anchorMin = path.anchorMax = path.pivot = Vector2.up;
            path.sizeDelta = MapSize;
            int count = Mathf.CeilToInt(Vector2.Distance(Centers[from], Centers[to]) / 24);
            var points = Handles.MakeBezierPoints(Centers[from], Centers[to], firstTangent, secondTangent, count + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 delta = points[i + 1] - points[i];
                Vector2 center = (points[i + 1] + points[i]) * .5f;
                var rect = Place("Ink", path, center, new Vector2(delta.magnitude * .42f, 3));
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
                var ink = rect.gameObject.AddComponent<DojoInkGraphic>();
                ink.color = new Color(.34f, .21f, .105f, .75f);
                ink.raycastTarget = false;
            }
        }

        private static void Tablet(TutorialView view, RectTransform choices, Sprite stone, int i)
        {
            var size = new Vector2(150, 56);
            var rect = Place(Ids[i], choices, Centers[i], size);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            view.lessonButtons[i] = button;

            var edge = Place("Focus Border", rect, size * .5f, new Vector2(155, 53));
            if (i != 0)
            {
                edge.localScale = new Vector3(0, 1, 1);
            }
            var border = edge.gameObject.AddComponent<Image>();
            border.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            border.type = Image.Type.Sliced;
            border.color = new Color(1, .86f, .44f);
            border.raycastTarget = false;
            var outline = edge.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(.30f, .18f, .08f, .85f);
            outline.effectDistance = new Vector2(1, -2);
            var focus = rect.gameObject.AddComponent<DojoControlFocus>();
            focus.control = button;
            focus.stroke = edge;

            var face = Place("Stone", rect, size * .5f, size).gameObject.AddComponent<Image>();
            face.sprite = stone;
            face.raycastTarget = false;
            var label = UiElements.Label("Lesson Name", rect, "", Vector2.zero, size, 24, UiTextRole.Title);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.075f, .11f, .14f);
            view.lessonLabels[i] = label;

            var stamp = Place("Completion Seal", rect, new Vector2(144, 5), new Vector2(40, 33));
            stamp.localRotation = Quaternion.Euler(0, 0, -8);
            var seal = stamp.gameObject.AddComponent<Image>();
            seal.color = new Color(.59f, .105f, .075f);
            seal.raycastTarget = false;
            var caption = UiElements.Label("Seal Text", stamp, "이수", Vector2.zero, stamp.sizeDelta, 16);
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = new Color(1, .95f, .81f);
            view.completionSeals[i] = stamp.gameObject;
            stamp.gameObject.SetActive(false);
        }

        private static RectTransform Place(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.up;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(center.x, -center.y);
            return rect;
        }

        private static void Navigate(Button button, Selectable left, Selectable right, Selectable up, Selectable down)
        {
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
                selectOnUp = up,
                selectOnDown = down,
            };
        }
    }
}
