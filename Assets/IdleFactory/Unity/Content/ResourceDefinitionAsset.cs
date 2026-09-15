using UnityEngine;

namespace IdleFactory.Unity.Content
{
    [CreateAssetMenu(menuName = "Idle Factory/Definitions/Resource", fileName = "ResourceDefinition")]
    public sealed class ResourceDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string developmentLabel;
        [SerializeField] private string presentationKey;
        [SerializeField] private bool isDevelopmentOnly;

        internal string Id => id;
        internal string DevelopmentLabel => developmentLabel;
        internal string PresentationKey => presentationKey;
        internal bool IsDevelopmentOnly => isDevelopmentOnly;
    }
}
