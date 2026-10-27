using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// "Is the pointer over a UI Toolkit element of any live screen panel?" (T366, F-B2). The world tap and drag path asks this at the start of a
    /// press so a press on UI never also acts on the world. It looks at every <see cref="UIDocument"/> that renders to the screen, not only the
    /// shell's own, so a separate panel (the development overlay, any later overlay) consumes the press too, without the world path knowing
    /// which panels exist. It works the same in release builds and references nothing but UI Toolkit.
    /// </summary>
    public static class RuntimePanelHitTest
    {
        /// <summary>
        /// True when an element other than a document root is picked at <paramref name="screenPoint"/> (pixels, origin bottom left) in
        /// <paramref name="primary"/> or in any other active document that renders to the screen. A document root covers its viewport and is
        /// never itself a hit. Documents that render to a texture are skipped, except <paramref name="primary"/>, which keeps the shell's
        /// previous behaviour.
        /// </summary>
        public static bool IsOverUi(Vector2 screenPoint, UIDocument primary)
        {
            if (HitsDocument(primary, screenPoint))
            {
                return true;
            }

            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null || document == primary)
                {
                    continue;
                }

                var settings = document.panelSettings;
                if (settings != null && settings.targetTexture != null)
                {
                    continue;
                }

                if (HitsDocument(document, screenPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HitsDocument(UIDocument document, Vector2 screenPoint)
        {
            var root = document == null ? null : document.rootVisualElement;
            var panel = root == null ? null : root.panel;
            if (panel == null)
            {
                return false;
            }

            var panelPoint = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            var picked = panel.Pick(panelPoint);
            return picked != null && picked != root;
        }
    }
}
