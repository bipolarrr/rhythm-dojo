using System.IO;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class TitleSkyLayoutBuilder
    {
        private const string Root = "Assets/Game/Art/TitleSky/";

        public static TitleSkyView Build(MainMenuView view)
        {
            var sky = view.gameObject.AddComponent<TitleSkyView>();
            var backdrop = Load("SkySource", UiArtworkCrop.Full);
            var connected = BakeConnection(backdrop, view.entrance.frame.GetComponent<Image>().sprite);
            var matte = Art("Sky Matte", view.transform, backdrop, Vector2.zero, Vector2.zero);
            Stretch(matte.rectTransform);
            matte.transform.SetSiblingIndex(view.backdrop.transform.GetSiblingIndex() + 1);
            sky.matte = matte.gameObject.AddComponent<CanvasGroup>();
            sky.matte.blocksRaycasts = false;
            var fit = matte.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;

            var entrance = (RectTransform)view.entrance.transform;
            entrance.gameObject.AddComponent<RectMask2D>();
            sky.travel = new GameObject("Sky To Dojo Travel", typeof(RectTransform)).GetComponent<RectTransform>();
            sky.travel.SetParent(entrance, false);
            Stretch(sky.travel);
            // Keep the existing door hierarchy, scale and entry sequence under one translating parent.
            for (int i = entrance.childCount - 2; i >= 0; i--)
            {
                var child = entrance.GetChild(i);
                child.SetParent(sky.travel, false);
                child.SetAsFirstSibling();
            }
            var background = Art("Sky Background", sky.travel, connected,
                new Vector2(0, 780), new Vector2(1280, 840));
            var cloudSprite = Load("Cloud", UiArtworkCrop.Alpha);
            sky.clouds = new RectTransform[3];
            for (int i = 0; i < sky.clouds.Length; i++)
            {
                float height = -230f;
                if (i == 1)
                {
                    height = -130f;
                }
                else if (i == 2)
                {
                    height = 280f;
                }
                var cloud = Art("Cloud " + i, background.transform, cloudSprite,
                    new Vector2(0, height), new Vector2(290 - i * 35, 95 - i * 10));
                cloud.preserveAspect = true;
                cloud.color = new Color(1f, 1f, 1f, .72f);
                sky.clouds[i] = cloud.rectTransform;
            }
            sky.birdFrames = BirdFrames();
            sky.birds = new Image[2];
            for (int i = 0; i < sky.birds.Length; i++)
            {
                sky.birds[i] = Art("Swallow " + i, background.transform, sky.birdFrames[0],
                    Vector2.zero, new Vector2(44 - i * 8, 44 - i * 8));
                sky.birds[i].preserveAspect = true;
            }
            var logo = Art("Sky Logo", entrance, DojoRenewalLayoutBuilder.Load("BrushTitle"),
                Vector2.zero, new Vector2(610, 205));
            logo.preserveAspect = true;
            sky.logo = logo.gameObject.AddComponent<CanvasGroup>();
            sky.logo.blocksRaycasts = false;
            var input = Art("Sky Start Input", view.transform, null, Vector2.zero, Vector2.zero);
            Stretch(input.rectTransform);
            input.color = Color.clear;
            input.raycastTarget = true;
            sky.start = input.gameObject.AddComponent<Button>();
            sky.start.targetGraphic = input;
            sky.start.transition = Selectable.Transition.None;
            var navigation = sky.start.navigation;
            navigation.mode = Navigation.Mode.None;
            sky.start.navigation = navigation;
            view.startPrompt.gameObject.SetActive(false);
            view.entranceInvitation.SetActive(false);
            sky.Validate();
            sky.ResetIdle();
            return sky;
        }

        private static Sprite BakeConnection(Sprite source, Sprite doorway)
        {
            // Bake only the new sky's lower edge. Neither the doorway nor its texture is changed.
            // Matching the exact top scanline avoids a horizontal color seam during the pan.
            var texture = source.texture;
            var pixels = texture.GetPixels();
            const int blendRows = 8;
            for (int y = 0; y < blendRows; y++)
            {
                float amount = Mathf.SmoothStep(0f, 1f, (float)y / (blendRows - 1));
                for (int x = 0; x < texture.width; x++)
                {
                    int edgeX = Mathf.Min(doorway.texture.width - 1,
                        Mathf.FloorToInt((float)x / texture.width * doorway.texture.width));
                    var edge = doorway.texture.GetPixel(edgeX, doorway.texture.height - 1);
                    edge.a = 1f;
                    pixels[y * texture.width + x] = Color.Lerp(edge, pixels[y * texture.width + x], amount);
                }
            }
            var generated = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                generated.SetPixels(pixels);
                generated.Apply();
                var path = Root + "Sky.png";
                var bytes = generated.EncodeToPNG();
                bool changed = !File.Exists(path);
                if (!changed)
                {
                    changed = !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes);
                }
                if (changed)
                {
                    File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                return UiArtworkAssets.Load(path, Root + "Sky.asset", "Sky", UiArtworkCrop.Full,
                    Vector4.zero, changed);
            }
            finally
            {
                Object.DestroyImmediate(generated);
            }
        }

        private static Sprite[] BirdFrames()
        {
            var sheet = Load("SwallowSheet", UiArtworkCrop.Full);
            var frames = new Sprite[6];
            float size = sheet.texture.width / 6f;
            for (int i = 0; i < frames.Length; i++)
            {
                string path = Root + "Swallow" + i + ".asset";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i])
                {
                    continue;
                }
                frames[i] = Sprite.Create(sheet.texture,
                    new Rect(i * size, (sheet.texture.height - size) * .5f, size, size),
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                frames[i].name = "Swallow" + i;
                AssetDatabase.CreateAsset(frames[i], path);
            }
            return frames;
        }

        private static Sprite Load(string name, UiArtworkCrop crop)
        {
            return UiArtworkAssets.Load(Root + name + ".png", Root + name + ".asset",
                name, crop, Vector4.zero);
        }

        private static Image Art(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
