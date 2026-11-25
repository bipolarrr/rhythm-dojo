using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class PresentationAssetBuilder
    {
        internal const string Art = "Assets/Game/Art/ArcadeField/";

        public static GeneratedContent Prepare(GameSettings settings)
        {
            PrepareFieldTemplates(settings.presentation);
            var content = new GeneratedContent
            {
                Settings = settings,
                TapTemplate = Template("TapNote", false),
                HoldTemplate = Template("HoldNote", true),
            };
            AssetDatabase.SaveAssets();
            return content;
        }

        public static void PrepareNoteTemplates()
        {
            Prepare(SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath));
        }

        internal static RectTransform Group(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Sprite Artwork(string name)
        {
            return SceneResources.Require<Sprite>(Art + name + ".png");
        }

        private static Image Part(string name, Transform parent, string artwork)
        {
            var rect = Group(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Artwork(artwork);
            image.raycastTarget = false;
            image.SetNativeSize();
            return image;
        }

        internal static PlayfieldPresenter BuildField(RectTransform host)
        {
            var viewport = host.gameObject.AddComponent<GameplayViewport>();
            viewport.field = Group("Lane Drawing", host);
            viewport.field.anchorMin = viewport.field.anchorMax = new Vector2(.5f, .5f);
            viewport.field.gameObject.AddComponent<RectMask2D>();
            viewport.Fit(4);

            var field = Group("Playfield", viewport.field).gameObject.AddComponent<PlayfieldPresenter>();
            field.viewport = viewport;
            field.grid = Group("Beat Grid", field.transform);
            field.judgmentLine = Part("Judgement Line", field.transform, "StrikeLine");
            field.judgmentLine.type = Image.Type.Tiled;
            return field;
        }

        private static NoteView Template(string name, bool hold)
        {
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.sizeDelta = Vector2.zero;
                var view = root.AddComponent<NoteView>();
                string artwork = "Tap";
                if (hold)
                {
                    artwork = "HoldHead";
                    var body = Part("Hold Body", rect, "WovenHold");
                    body.type = Image.Type.Tiled;
                    body.rectTransform.pivot = new Vector2(.5f, 0);
                    SceneDependencyAssembler.Wire(view, "body", body.rectTransform);
                    var marker = Part("Hold End Marker", rect, "HoldEnd");
                    SceneDependencyAssembler.Wire(view, "tailMarker", marker.rectTransform);
                }
                var head = Part("Head", rect, artwork);
                SceneDependencyAssembler.Wire(view, "head", head.rectTransform);
                return PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Game/Prefabs/{name}.prefab")
                    .GetComponent<NoteView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PrepareFieldTemplates(GameplayPresentationSettings settings)
        {
            var image = Part("Field Mark", null, "InkField");
            try
            {
                settings.fieldTemplate = PrefabUtility.SaveAsPrefabAsset(image.gameObject,
                    "Assets/Game/Prefabs/FieldMark.prefab").GetComponent<Image>();
            }
            finally
            {
                Object.DestroyImmediate(image.gameObject);
            }

            settings.highway = Artwork("InkField");
            settings.divider = Artwork("Divider");
            settings.border = Artwork("BoundEdge");
            settings.strikeShoe = Artwork("Receptor");
            settings.pressedShoe = Artwork("PressedReceptor");
            settings.judgmentLine = Artwork("StrikeLine");
            settings.beat = Artwork("Beat");
            settings.measure = Artwork("Measure");
            EditorUtility.SetDirty(settings);
        }
    }
}
