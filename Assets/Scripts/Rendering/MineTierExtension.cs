using System;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>MineEmbers-only tier knobs, kept out of the shared QualityTiers package.</summary>
    [Serializable]
    public class MineTierExtension : TierExtension
    {
        public bool heatHaze = true;
        public bool lakeReflection = true;
    }
}
