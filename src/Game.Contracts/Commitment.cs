namespace Game.Contracts;

/// <summary>One reason to carry on with what you are already doing.</summary>
public readonly record struct CommitmentTerm(int Weight, string Reason);

/// <summary>
/// The weighed case for NOT stopping — the incumbent activity's defence
/// against whatever the weight pipeline just offered instead.
/// </summary>
/// <remarks>
/// Resolved by COMPARISON against the challenger's total, never by folding the
/// incumbent into the weighted roll as one more candidate. The difference is
/// not stylistic: a shift defended at 60 against food weighted 20 would be
/// abandoned with probability 0.25 on EVERY beat it is re-asked, and across a
/// dozen beats that is not a risk, it is a certainty. Per-beat rolls compound;
/// thresholds do not.
/// <para>
/// Terms carry their reason so the inspect UI and the decision probe can say
/// why somebody stayed at work, the same way <see cref="ActionWeight"/> says
/// why they went.
/// </para>
/// </remarks>
public sealed class Commitment
{
    public IReadOnlyList<CommitmentTerm> Terms { get; init; } = [];

    public int Total
    {
        get
        {
            int sum = 0;
            foreach (var term in Terms) sum += term.Weight;
            return sum;
        }
    }

    /// <summary>The loudest single reason, for the one-line explanation.</summary>
    public CommitmentTerm? Primary
    {
        get
        {
            CommitmentTerm? best = null;
            foreach (var term in Terms)
                if (best is null || Math.Abs(term.Weight) > Math.Abs(best.Value.Weight)) best = term;
            return best;
        }
    }

    public string Explain() =>
        $"carry on = {Total}  [" +
        string.Join(' ', Terms.Select(t => $"{(t.Weight >= 0 ? "+" : "")}{t.Weight} {t.Reason}")) +
        "]";
}
