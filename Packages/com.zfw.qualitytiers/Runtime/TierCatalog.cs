using UnityEngine;

namespace Zfw.QualityTiers
{
    /// <summary>The four TierSettings, loaded from Resources/TierCatalog.</summary>
    [CreateAssetMenu(menuName = "Zfw/Tier Catalog", fileName = "TierCatalog")]
    public class TierCatalog : ScriptableObject
    {
        public TierSettings low, medium, high, ultra;

        public TierSettings Get(Tier t) => t switch
        {
            Tier.Low => low, Tier.Medium => medium, Tier.High => high, _ => ultra,
        };
    }
}
