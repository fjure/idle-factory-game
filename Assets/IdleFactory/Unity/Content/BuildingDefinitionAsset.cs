using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Content
{
    [CreateAssetMenu(menuName = "Idle Factory/Definitions/Building", fileName = "BuildingDefinition")]
    public sealed class BuildingDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class ResourceCapacityRecord
        {
            [SerializeField] private ResourceDefinitionAsset resource;
            [SerializeField] private long capacity;

            public ResourceCapacityRecord()
            {
            }

            public ResourceCapacityRecord(ResourceDefinitionAsset resource, long capacity)
            {
                this.resource = resource;
                this.capacity = capacity;
            }

            internal ResourceDefinitionAsset Resource => resource;
            internal long Capacity => capacity;
        }

        [SerializeField] private string id;
        [SerializeField] private int footprintWidth;
        [SerializeField] private int footprintHeight;
        [SerializeField] private List<int> allowedOrientations = new List<int>();
        [SerializeField] private RecipeDefinitionAsset recipe;
        [SerializeField] private bool hasInputBuffer;
        [SerializeField] private List<ResourceCapacityRecord> inputCapacities = new List<ResourceCapacityRecord>();
        [SerializeField] private bool hasOutputBuffer;
        [SerializeField] private List<ResourceCapacityRecord> outputCapacities = new List<ResourceCapacityRecord>();
        [SerializeField] private string presentationKey;
        [SerializeField] private bool isDevelopmentOnly;

        internal string Id => id;
        internal int FootprintWidth => footprintWidth;
        internal int FootprintHeight => footprintHeight;
        internal List<int> AllowedOrientations => allowedOrientations;
        internal RecipeDefinitionAsset Recipe => recipe;
        internal bool HasInputBuffer => hasInputBuffer;
        internal List<ResourceCapacityRecord> InputCapacities => inputCapacities;
        internal bool HasOutputBuffer => hasOutputBuffer;
        internal List<ResourceCapacityRecord> OutputCapacities => outputCapacities;
        internal string PresentationKey => presentationKey;
        internal bool IsDevelopmentOnly => isDevelopmentOnly;
    }
}
