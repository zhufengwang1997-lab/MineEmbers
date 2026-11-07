namespace Zfw.QualityTiers
{
    // Ordered: a numerically smaller tier is cheaper. min(a, b) picks the more conservative one.
    public enum Tier { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    public enum SelectionMode { Auto, Manual, CommandLine }
}
