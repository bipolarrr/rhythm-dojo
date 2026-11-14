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
            var backdrop = Load("LandscapeSource", UiArtworkCrop.Full);
            var connected = BakeConnection(backdrop, view.entrance.frame.GetComponent<Image>().sprite);
            var doorway = view.entrance.frame.GetComponent<Image>();
            float doorwayHeight = doorway.sprite.texture.height;
            float skyHeight = connected.texture.height - doorwayHeight;
            doorway.sprite = Slice("SkyEntrance", connected, new Rect(0, 0, connected.texture.width, doorwayHeight));
            var upper = Slice("SkyUpper", connected,
                new Rect(0, doorwayHeight, connected.texture.width, skyHeight));
            sky.descentDistance = 720f * skyHeight / doorwayHeight;
            var matte = Art("Sky Matte", view.transform, Load("SkySource", UiArtworkCrop.Full), Vector2.zero, Vector2.zero);
            Stretch(matte.rectTransform);
            matte.transform.SetSiblingIndex(view.backdrop.transform.GetSiblingIndex() + 1);
            sky.matte = matte.gameObject.AddComponent<CanvasGroup>();
            sky.matte.blocksRaycasts = false;
            var fit = matte.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;

            var entrance = (RectTransform)view.entrance.transform;
            // Crop the complete journey as one composition instead of exposing an unrelated
            // room in aspect-ratio margins while the outdoor camera is still descending.
            var composition = entrance.GetComponent<DojoCompositionFit>();
            composition.coverViewport = true;
            composition.Fit();
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
            // Both regions share one baked landscape and exactly the same pixel scale.
            var background = Art("Sky Background", sky.travel, upper,
                new Vector2(0, 360f + sky.descentDistance * .5f), new Vector2(1280, sky.descentDistance));
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
            var logo = Art("Sky Logo", view.transform, DojoRenewalLayoutBuilder.Load("BrushTitle"),
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
            // The outpaint supplies sky, complete pines and the roof above the wooden facade.
            // Restore the facade from the plaque's top edge down, including doorway alpha,
            // byte for byte. This is a content boundary, with no color blending or seam overlay.
            var texture = source.texture;
            int width = doorway.texture.width;
            int height = doorway.texture.height + Mathf.RoundToInt(doorway.texture.height * 840f / 720f);
            var pixels = new Color32[width * height];
            var sourcePixels = texture.GetPixels32();
            var doorwayPixels = doorway.texture.GetPixels32();
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(texture.height - 1, Mathf.FloorToInt((y + .5f) * texture.height / height));
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(texture.width - 1, Mathf.FloorToInt((x + .5f) * texture.width / width));
                    var pixel = sourcePixels[sourceY * texture.width + sourceX];
                    pixel.a = 255;
                    if (y < doorway.texture.height - 190)
                    {
                        var original = doorwayPixels[y * width + x];
                        pixel = original;
                    }
                    pixels[y * width + x] = pixel;
                }
            }
            var generated = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                generated.SetPixels32(pixels);
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

        private static Sprite Slice(string name, Sprite source, Rect region)
        {
            string path = Root + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite && sprite.texture == source.texture && sprite.rect == region)
            {
                return sprite;
            }

            var generated = Sprite.Create(source.texture, region, new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect);
            generated.name = name;
            if (sprite)
            {
                EditorUtility.CopySerialized(generated, sprite);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(sprite);
                AssetDatabase.SaveAssetIfDirty(sprite);
                return sprite;
            }

            AssetDatabase.CreateAsset(generated, path);
            return generated;
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
