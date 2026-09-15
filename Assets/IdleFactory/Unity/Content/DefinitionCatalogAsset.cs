using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Content
{
    [CreateAssetMenu(menuName = "Idle Factory/Definitions/Catalog", fileName = "DefinitionCatalog")]
    public sealed class DefinitionCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<ResourceDefinitionAsset> resources = new List<ResourceDefinitionAsset>();
        [SerializeField] private List<RecipeDefinitionAsset> recipes = new List<RecipeDefinitionAsset>();
        [SerializeField] private List<BuildingDefinitionAsset> buildings = new List<BuildingDefinitionAsset>();
        [SerializeField] private List<OfflinePolicyAsset> offlinePolicies = new List<OfflinePolicyAsset>();

        internal List<ResourceDefinitionAsset> Resources => resources;
        internal List<RecipeDefinitionAsset> Recipes => recipes;
        internal List<BuildingDefinitionAsset> Buildings => buildings;
        internal List<OfflinePolicyAsset> OfflinePolicies => offlinePolicies;
    }
}
