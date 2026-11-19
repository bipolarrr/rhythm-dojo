using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    // Applied at the shared scene/template authoring boundary, including inactive dropdown and row templates.
    public static class DojoHoverBuilder
    {
        public static void Prepare(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                var control = graphic.GetComponentInParent<Selectable>(true);
                if (!control)
                {
                    continue;
                }
                var settings = control.GetComponentInParent<SettingsView>(true);
                if (settings && control == settings.dismiss)
                {
                    continue;
                }
                if (control is Dropdown dropdown && dropdown.template)
                {
                    if (graphic.transform.IsChildOf(dropdown.template))
                    {
                        var previous = graphic.GetComponent<DojoHoverGraphic>();
                        if (previous)
                        {
                            Object.DestroyImmediate(previous);
                        }
                        continue;
                    }
                }
                var focus = control.GetComponent<DojoControlFocus>();
                if (focus && graphic.transform == focus.stroke)
                {
                    continue;
                }
                Bind(graphic, control);
            }

            foreach (var sky in root.GetComponentsInChildren<TitleSkyView>(true))
            {
                // The title accepts a click anywhere; emphasize its logo, never the full-screen hit surface.
                Bind(sky.logo.GetComponent<Graphic>(), sky.start);
            }
        }

        private static void Bind(Graphic graphic, Selectable control)
        {
            var effect = graphic.GetComponent<DojoHoverGraphic>();
            if (!effect)
            {
                effect = graphic.gameObject.AddComponent<DojoHoverGraphic>();
                EnlargeButtonGraphic(graphic, control);
            }
            effect.control = control;
        }

        public static void EnlargeButtonGraphic(Graphic graphic, Selectable control)
        {
            if (!(control is Button))
            {
                return;
            }
            if (graphic is Text label)
            {
                // Large room headings already act as artwork; this pass improves small button captions.
                if (label.fontSize > 48)
                {
                    return;
                }
                int maximum = label.resizeTextMaxSize;
                label.fontSize = Mathf.RoundToInt(label.fontSize * 1.10f);
                if (label.resizeTextForBestFit)
                {
                    label.resizeTextMaxSize = Mathf.RoundToInt(maximum * 1.10f);
                }
                return;
            }
            if (!(graphic is Image image) || !image.sprite)
            {
                return;
            }
            // Enlarge small decorative icons inside their existing button, preserving the hit surface and layout.
            if (graphic.raycastTarget || graphic.transform == control.transform)
            {
                return;
            }
            var rect = graphic.rectTransform;
            if (rect.anchorMin != rect.anchorMax || rect.childCount != 0)
            {
                return;
            }
            if (rect.rect.width > 64 || rect.rect.height > 64)
            {
                return;
            }
            rect.sizeDelta *= 1.10f;
        }
    }
}
