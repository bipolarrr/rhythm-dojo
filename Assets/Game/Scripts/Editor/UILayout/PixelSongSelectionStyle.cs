using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class PixelSongSelectionStyle
    {
        public static PixelDojoFrameGraphic Frame(RectTransform parent, Color fill, float width)
        {
            var image = parent.GetComponent<Image>();
            if (image)
            {
                image.color = Color.clear;
                var gradient = image as SelectionGradientImage;
                if (gradient)
                {
                    gradient.useGradient = false;
                }
            }
            var go = new GameObject("Pixel Frame", typeof(RectTransform), typeof(PixelDojoFrameGraphic));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            Bounds((RectTransform)go.transform, 0, 0, 1, 1);
            var frame = go.GetComponent<PixelDojoFrameGraphic>();
            frame.color = fill;
            frame.borderWidth = width;
            frame.raycastTarget = false;
            var button = parent.GetComponent<Button>();
            if (button)
            {
                button.targetGraphic = frame;
            }
            return frame;
        }

        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
