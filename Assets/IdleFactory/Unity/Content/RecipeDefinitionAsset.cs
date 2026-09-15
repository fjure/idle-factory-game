using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Content
{
    [CreateAssetMenu(menuName = "Idle Factory/Definitions/Recipe", fileName = "RecipeDefinition")]
    public sealed class RecipeDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class ResourceAmountRecord
        {
            [SerializeField] private ResourceDefinitionAsset resource;
            [SerializeField] private long amount;

            public ResourceAmountRecord()
            {
            }

            public ResourceAmountRecord(ResourceDefinitionAsset resource, long amount)
            {
                this.resource = resource;
                this.amount = amount;
            }

            internal ResourceDefinitionAsset Resource => resource;
            internal long Amount => amount;
        }

        [SerializeField] private string id;
        [SerializeField] private List<ResourceAmountRecord> inputs = new List<ResourceAmountRecord>();
        [SerializeField] private List<ResourceAmountRecord> outputs = new List<ResourceAmountRecord>();
        [SerializeField] private long durationTicks;
        [SerializeField] private bool isDevelopmentOnly;

        internal string Id => id;
        internal List<ResourceAmountRecord> Inputs => inputs;
        internal List<ResourceAmountRecord> Outputs => outputs;
        internal long DurationTicks => durationTicks;
        internal bool IsDevelopmentOnly => isDevelopmentOnly;
    }
}
