using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class DojoRenewalLayoutBuilder
    {
        public const string Root = "Assets/Game/Art/DojoRenewal/";

        public static Sprite Load(string name)
        {
            return UiArtworkAssets.Load(Root + name + ".png", Root + name + ".asset",
                name, UiArtworkCrop.Full, Vector4.zero);
        }

        public static void BuildEntrance(MainMenuView view)
        {
            var entrance = view.entrance;
            Object.DestroyImmediate(entrance.GetComponent<AspectRatioFitter>());
            entrance.gameObject.AddComponent<DojoCompositionFit>().Fit();
            var matte = Image("Dojo Canvas Matte", view.transform, Load("PracticeHall"));
            view.backdrop = matte;
            if (view.modeSelection)
            {
                view.modeSelection.canvasBackdrop = matte;
                view.modeSelection.trainingBackdrop = matte.sprite;
            }
            matte.transform.SetAsFirstSibling();
            matte.color = new Color(.35f, .40f, .38f);
            matte.rectTransform.anchorMin = Vector2.zero;
            matte.rectTransform.anchorMax = Vector2.one;
            matte.rectTransform.offsetMin = matte.rectTransform.offsetMax = Vector2.zero;
            var matteFit = matte.gameObject.AddComponent<AspectRatioFitter>();
            matteFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            matteFit.aspectRatio = 16f / 9f;
            var approach = new GameObject("Doorway Approach", typeof(RectTransform));
            approach.transform.SetParent(entrance.transform, false);
            entrance.doorwayApproach = (RectTransform)approach.transform;
            entrance.doorwayApproach.anchorMin = Vector2.zero;
            entrance.doorwayApproach.anchorMax = Vector2.one;
            entrance.doorwayApproach.offsetMin = entrance.doorwayApproach.offsetMax = Vector2.zero;
            entrance.frame.transform.SetParent(approach.transform, false);
            entrance.frame.GetComponent<Image>().sprite = Load("HanokEntrance");
            var doors = Load("HanokDoors");
            var left = Slice("HanokLeftDoor", doors, new Rect(0, 0, .5f, 1));
            var right = Slice("HanokRightDoor", doors, new Rect(.5f, 0, .5f, 1));
            var aperture = new GameObject("Door Aperture", typeof(RectTransform), typeof(RectMask2D));
            aperture.transform.SetParent(approach.transform, false);
            aperture.transform.SetAsFirstSibling();
            var rect = (RectTransform)aperture.transform;
            rect.anchorMin = new Vector2(.246f, .145f);
            rect.anchorMax = new Vector2(.754f, .672f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            entrance.leftDoor.SetParent(rect, false);
            entrance.rightDoor.SetParent(rect, false);
            entrance.leftDoor.GetComponent<Image>().sprite = left;
            entrance.rightDoor.GetComponent<Image>().sprite = right;
            entrance.rightDoor.localScale = Vector3.one;
            entrance.leftDoor.anchorMin = Vector2.zero;
            entrance.leftDoor.anchorMax = new Vector2(.5f, 1);
            entrance.rightDoor.anchorMin = new Vector2(.5f, 0);
            entrance.rightDoor.anchorMax = Vector2.one;
            entrance.leftDoor.offsetMin = entrance.leftDoor.offsetMax = Vector2.zero;
            entrance.rightDoor.offsetMin = entrance.rightDoor.offsetMax = Vector2.zero;

            view.ambientLights = System.Array.Empty<Image>();
            Place((RectTransform)view.logo.transform, new Vector2(.5f, .746f), Vector2.zero, new Vector2(410, 70));
            view.logoArtwork.gameObject.SetActive(false);
            view.title.enabled = true;
            view.title.color = new Color(.035f, .025f, .015f);
            view.title.fontSize = 64;
            view.logoArtwork = view.title.rectTransform;
            Place(view.logoArtwork, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(370, 80));
            view.title.alignment = TextAnchor.MiddleCenter;
            var prompt = Image("Entrance Invitation", entrance.frame.transform, null);
            prompt.color = Color.clear;
            view.entranceInvitation = prompt.gameObject;
            Place(prompt.rectTransform, new Vector2(1f, 0f), new Vector2(-290, 38), new Vector2(520, 40));
            view.startPrompt.transform.SetParent(prompt.transform, false);
            Place(view.startPrompt.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 40));
            view.startPrompt.fontSize = 18;
            view.startPrompt.alignment = TextAnchor.MiddleRight;
            view.startPrompt.color = new Color(1f, .94f, .78f);
            foreach (var shadow in view.startPrompt.GetComponents<Shadow>())
            {
                shadow.enabled = true;
            }
            AddRoomAtmosphere(entrance.frame.transform, -1);
        }

        public static void AddRoomAtmosphere(Transform parent, int index)
        {
            if (index == 1 || index == 3)
            {
                return;
            }
            var motion = parent.gameObject.AddComponent<DojoAmbientMotion>();
            motion.drift = new Vector2(3, -1);
            var color = new Color(1, .91f, .72f, .18f);
            if (index == 2)
            {
                motion.drift = new Vector2(22, -2);
                color = new Color(.87f, .95f, 1, .20f);
            }
            AddPetals(motion, 3, color);
        }

        private static void AddPetals(DojoAmbientMotion motion, int count, Color color)
        {
            motion.petals = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Airborne Dust " + i, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(DojoPetalGraphic));
                go.transform.SetParent(motion.transform, false);
                var graphic = go.GetComponent<DojoPetalGraphic>();
                graphic.color = color;
                graphic.raycastTarget = false;
                var rect = (RectTransform)go.transform;
                Place(rect, new Vector2(.5f, .5f),
                    new Vector2(-480 + (i * 397 % 960), -180 + (i * 211 % 500)), new Vector2(2, 2));
                motion.petals[i] = rect;
            }
        }

        public static Sprite Slice(string name, Sprite source, Rect region)
        {
            string path = Root + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }
            var rect = source.rect;
            rect = new Rect(rect.x + rect.width * region.x, rect.y + rect.height * region.y,
                rect.width * region.width, rect.height * region.height);
            sprite = Sprite.Create(source.texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Image Image(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
