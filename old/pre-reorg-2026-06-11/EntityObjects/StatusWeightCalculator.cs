namespace EntityObjects;

/// <summary>
/// Pure-function calculator that converts an entity's current HP, Hunger,
/// and Mood into additive Status weights on action candidates.
///
/// All matching is keyword-based — the calculator never checks action names.
/// Adding a new action with the right keywords automatically picks up all
/// status modifiers with zero changes here.
///
/// Weights activate in two bands:
///   - Deficit  (ratio &lt; 0.50): self-preserving — greedy, defensive, antisocial
///   - Exceeded (ratio &gt; 0.75): overindulgent  — giddy, complacent, overly social
///
/// The sweet spot is the middle band (0.50–0.75) where the entity behaves
/// normally and the player has the most control.
/// </summary>
public static class StatusWeightCalculator
{
    // ── Thresholds ──────────────────────────────────────────────────────
    private const double MildThreshold = 0.50;
    private const double CriticalThreshold = 0.25;
    private const double ExceededThreshold = 0.75;

    // ── Deficit scales (per matching keyword) ───────────────────────────
    // Low HP: boost SelfPreserving/Defensive, penalize Offensive/Aggressive
    private const double HpSelfPreservingScale = 8;
    private const double HpDefensiveScale = 6;
    private const double HpOffensivePenaltyScale = 5;

    // Low hunger: boost Greedy/SelfPreserving, penalize outward actions
    private const double HungerGreedyScale = 12;
    private const double HungerSelfPreservingScale = 4;
    private const double HungerOffensivePenaltyScale = 3;
    private const double HungerSocialPenaltyScale = 3;

    // Low mood: boost SelfPreserving, penalize Social/Diplomatic
    private const double MoodSelfPreservingScale = 5;
    private const double MoodSocialPenaltyScale = 6;

    // ── Exceeded scales (per matching keyword) ──────────────────────────
    // Giddy mood: boost Social/Diplomatic, penalize Aggressive/Violent
    private const double GiddySocialScale = 5;
    private const double GiddyAggressivePenaltyScale = 4;

    // Stuffed hunger: boost Social, penalize Aggressive/Defensive
    private const double ComplacentSocialScale = 3;
    private const double ComplacentAggressivePenaltyScale = 3;
    private const double ComplacentDefensivePenaltyScale = 2;

    // ── Keyword → (scale, isBoost) mappings for each condition ──────────
    // These define which keywords are affected under each status condition.
    // The calculator counts how many of the action's keywords match and
    // multiplies: scale * matchCount * deficit/excess.

    private static readonly (string Keyword, double Scale)[] HpDeficitBoosts =
        [("SelfPreserving", HpSelfPreservingScale), ("Defensive", HpDefensiveScale)];

    private static readonly (string Keyword, double Scale)[] HpCriticalPenalties =
        [("Offensive", HpOffensivePenaltyScale), ("Aggressive", HpOffensivePenaltyScale)];

    private static readonly (string Keyword, double Scale)[] HungerDeficitBoosts =
        [("Greedy", HungerGreedyScale), ("SelfPreserving", HungerSelfPreservingScale)];

    private static readonly (string Keyword, double Scale)[] HungerCriticalPenalties =
        [("Offensive", HungerOffensivePenaltyScale), ("Aggressive", HungerOffensivePenaltyScale),
         ("Social", HungerSocialPenaltyScale), ("Diplomatic", HungerSocialPenaltyScale)];

    private static readonly (string Keyword, double Scale)[] MoodDeficitBoosts =
        [("SelfPreserving", MoodSelfPreservingScale)];

    private static readonly (string Keyword, double Scale)[] MoodDeficitPenalties =
        [("Social", MoodSocialPenaltyScale), ("Diplomatic", MoodSocialPenaltyScale)];

    private static readonly (string Keyword, double Scale)[] GiddyBoosts =
        [("Social", GiddySocialScale), ("Diplomatic", GiddySocialScale)];

    private static readonly (string Keyword, double Scale)[] GiddyPenalties =
        [("Aggressive", GiddyAggressivePenaltyScale), ("Violent", GiddyAggressivePenaltyScale)];

    private static readonly (string Keyword, double Scale)[] ComplacentBoosts =
        [("Social", ComplacentSocialScale)];

    private static readonly (string Keyword, double Scale)[] ComplacentPenalties =
        [("Aggressive", ComplacentAggressivePenaltyScale), ("Violent", ComplacentAggressivePenaltyScale),
         ("Defensive", ComplacentDefensivePenaltyScale)];

    public static void Apply(Entity entity, List<ActionCandidate> candidates)
    {
        double hpRatio = entity.GetCurrentHealth() / entity.GetMaxHealth();
        double hungerRatio = entity.Needs.HungerRatio;
        double moodRatio = entity.Needs.MoodRatio;

        foreach (var candidate in candidates)
        {
            int weight = 0;
            var keywords = candidate.Action.Keywords;

            // ── HP deficit ──────────────────────────────────────────
            if (hpRatio < MildThreshold)
            {
                double deficit = 1.0 - hpRatio;
                weight += ScaleByKeywords(keywords, HpDeficitBoosts, deficit);

                if (hpRatio < CriticalThreshold)
                    weight -= ScaleByKeywords(keywords, HpCriticalPenalties, CriticalThreshold - hpRatio);
            }

            // ── Hunger deficit ──────────────────────────────────────
            if (hungerRatio < MildThreshold)
            {
                double deficit = 1.0 - hungerRatio;
                weight += ScaleByKeywords(keywords, HungerDeficitBoosts, deficit);

                if (hungerRatio < CriticalThreshold)
                    weight -= ScaleByKeywords(keywords, HungerCriticalPenalties, CriticalThreshold - hungerRatio);
            }

            // ── Mood deficit ────────────────────────────────────────
            if (moodRatio < MildThreshold)
            {
                double deficit = 1.0 - moodRatio;
                weight += ScaleByKeywords(keywords, MoodDeficitBoosts, deficit);
                weight -= ScaleByKeywords(keywords, MoodDeficitPenalties, MildThreshold - moodRatio);
            }

            // ── Mood exceeded (giddy) ───────────────────────────────
            if (moodRatio > ExceededThreshold)
            {
                double excess = moodRatio - ExceededThreshold;
                weight += ScaleByKeywords(keywords, GiddyBoosts, excess);
                weight -= ScaleByKeywords(keywords, GiddyPenalties, excess);
            }

            // ── Hunger exceeded (complacent) ────────────────────────
            if (hungerRatio > ExceededThreshold)
            {
                double excess = hungerRatio - ExceededThreshold;
                weight += ScaleByKeywords(keywords, ComplacentBoosts, excess);
                weight -= ScaleByKeywords(keywords, ComplacentPenalties, excess);
            }

            if (weight != 0)
            {
                candidate.Weights.Add(new ActionWeight
                {
                    Weight = weight,
                    Source = ActionWeightSource.Status
                });
            }
        }
    }

    /// <summary>
    /// For each keyword rule, check if the action has that keyword.
    /// Sum up: scale * magnitude for each match.
    /// </summary>
    private static int ScaleByKeywords(
        HashSet<string> actionKeywords,
        (string Keyword, double Scale)[] rules,
        double magnitude)
    {
        double total = 0;
        foreach (var (keyword, scale) in rules)
        {
            if (actionKeywords.Contains(keyword))
                total += scale * magnitude;
        }
        return (int)total;
    }
}
