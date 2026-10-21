using System;
using System.Collections.Generic;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>The set of content IDs of a catalog (S3): resources, machines, contract offers, Items, Research and industry upgrades, plus the two expansion targets.</summary>
    public sealed class SessionLogCatalogIds : ISessionLogContentIds
    {
        /// <summary>The fixed targets of a Storage or Factory purchase record.</summary>
        public const string StorageTarget = "storage";

        public const string FactoryTarget = "factory";

        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);

        public SessionLogCatalogIds(RunCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            AddAll(catalog.Resources.Keys);
            AddAll(catalog.Machines.Keys);
            AddAll(catalog.ContractOffers.Keys);
            AddAll(catalog.Items.Keys);
            AddAll(catalog.Research.Keys);
            AddAll(catalog.IndustryUpgrades.Keys);
            _ids.Add(StorageTarget);
            _ids.Add(FactoryTarget);
        }

        public int Count => _ids.Count;

        public bool Contains(string id) => id != null && _ids.Contains(id);

        private void AddAll(IEnumerable<string> ids)
        {
            foreach (string id in ids) _ids.Add(id);
        }
    }
}
