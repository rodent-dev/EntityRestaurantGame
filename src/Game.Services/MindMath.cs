namespace Game.Services;

/// <summary>
/// The three curves the whole dialog/desire system leans on. Soft, never
/// cliff: the logistic IS the "randomized yet predictable threshold" —
/// traits set where the knee sits (predictable), the draw is stochastic.
/// </summary>
public static class MindMath
{
    public static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

    /// <summary>
    /// Satisfaction curve: diminishing returns on stacked descriptors.
    /// Satiation falls out of the counterfactual delta for free — an
    /// already-armored paladin sits on the flat part, so one more breastplate
    /// moves sat() barely at all.
    /// </summary>
    public static double Sat(double x) => Math.Tanh(x);

    /// <summary>
    /// Softmax selection over scored candidates. Temperature widens or
    /// sharpens the distribution; scores may be any real numbers.
    /// </summary>
    public static T? SoftmaxPick<T>(IReadOnlyList<(T Item, double Score)> scored, double temperature, Random rng)
        where T : class
    {
        if (scored.Count == 0) return null;

        double max = scored.Max(s => s.Score);
        var weights = scored.Select(s => Math.Exp((s.Score - max) / temperature)).ToArray();
        double total = weights.Sum();
        double roll = rng.NextDouble() * total;

        double cumulative = 0;
        for (int i = 0; i < scored.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return scored[i].Item;
        }
        return scored[^1].Item;
    }
}
