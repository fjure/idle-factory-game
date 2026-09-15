using UnityEngine;

namespace IdleFactory.Unity.Content
{
    [CreateAssetMenu(menuName = "Idle Factory/Definitions/Offline Policy", fileName = "OfflinePolicy")]
    public sealed class OfflinePolicyAsset : ScriptableObject
    {
        [SerializeField] private int revision;
        [SerializeField] private long efficiencyNumerator;
        [SerializeField] private long efficiencyDenominator;
        [SerializeField] private long maxCreditedTicks;

        internal int Revision => revision;
        internal long EfficiencyNumerator => efficiencyNumerator;
        internal long EfficiencyDenominator => efficiencyDenominator;
        internal long MaxCreditedTicks => maxCreditedTicks;
    }
}
