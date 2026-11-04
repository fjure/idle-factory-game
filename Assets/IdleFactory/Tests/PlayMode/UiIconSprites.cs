#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T441: the restyled UI icons (F004 <c>ui-icons</c> family, imported by T440) by presentation key. Every panel test rig resolves its icons
    /// here, so the layout and interaction suites run with the sprites the scene catalog serves, never with the Feature 003 placeholders.
    /// </summary>
    public static class UiIconSprites
    {
        public const string Root = "Assets/IdleFactory/Art/F004/ui-icons/";

        /// <summary>The first frame of the key (<c>F003-UI-ICON-CASH</c> is <c>f003-ui-icon-cash_00.png</c>), or null for an unknown key.</summary>
        public static Sprite Load(string key) =>
            key == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(Root + key.ToLowerInvariant() + "_00.png");

        /// <summary>The presentation key of a sprite loaded by <see cref="Load"/>, or null when the sprite is not a restyled UI icon.</summary>
        public static string KeyOf(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(sprite);
            if (path == null || !path.StartsWith(Root, System.StringComparison.Ordinal))
            {
                return null;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name.EndsWith("_00", System.StringComparison.Ordinal) ? name.Substring(0, name.Length - 3).ToUpperInvariant() : name.ToUpperInvariant();
        }

        /// <summary>True for a sprite of the Feature 003 placeholder set (the fallback that must not appear on a restyled surface).</summary>
        public static bool IsPlaceholder(Sprite sprite)
        {
            var path = sprite == null ? null : AssetDatabase.GetAssetPath(sprite);
            return path != null && path.Contains("/Art/Development/");
        }
    }
}
#endif
