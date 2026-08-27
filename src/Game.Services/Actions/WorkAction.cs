using System.Numerics;
using Engine.Spatial;
using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>What a job is: where it happens, what it makes, how long a shift
/// runs, what a good one is worth, and when to stop taking them.</summary>
/// <remarks>
/// <paramref name="ShiftBeats"/> and <paramref name="ShiftYield"/> are what turn
/// this from a verb into a job. A shift is authored as a TOTAL — "eight beats
/// on the bench makes four loaves" — and the sim divides; authoring a per-beat
/// rate instead would make the sentence you actually want to write
/// ("a shift pays four") unspeakable.
/// </remarks>
public sealed record WorkSpec(
    Location Workplace,
    IReadOnlyList<string> Produces,
    int MaxStock,
    IReadOnlyList<string> Keywords,
    int ShiftBeats = WorkAction.DefaultShiftBeats,
    int ShiftYield = WorkAction.DefaultShiftYield);

/// <summary>
/// A job as an action CANDIDATE — it competes in the same weighted selection
/// as everything else, so working is a choice, never an assignment. Keywords
/// come from the work spec (jobs suit different natures: a cook's work reads
/// Industrious/Pride/Greedy, a patrol would read Industrious/Defensive).
/// </summary>
/// <remarks>
/// The reference long-running action, and the reason
/// <see cref="IAction.Duration"/>, <see cref="IAction.Sustain"/> and
/// <see cref="IAction.Resolve"/> exist. A shift is something you are IN for
/// several beats: choosing it once buys the whole run, the commitment weighing
/// defends it against every subsequent beat's temptations, and what it pays
/// depends on how much of the shift was actually worked.
/// <para>
/// Three separate things can shrink the payout, which is the whole point of
/// splitting elapsed from progress: walking out early (fewer beats), commuting
/// (beats that elapse without counting), and turning up wrecked (beats that
/// count for a fraction). None of them is a special case here — they are all
/// just <see cref="Activity.Progress"/> failing to keep up with
/// <see cref="Activity.Elapsed"/>.
/// </para>
/// </remarks>
public sealed class WorkAction : IAction
{
    /// <summary>A shift, in decision beats. Long enough that leaving early means something.</summary>
    public const int DefaultShiftBeats = 8;

    /// <summary>What a shift worked properly, start to finish, produces.</summary>
    public const int DefaultShiftYield = 4;

    /// <summary>Mood cost of one beat at the bench. Work is work.</summary>
    public const int MoodPerBeat = -1;

    private readonly WorkSpec _spec;
    private readonly Func<string, IItem> _produceItem;
    private readonly Random _rng;

    /// <summary>Pass an <paramref name="rng"/> to keep production reproducible —
    /// which good comes off the bench changes what everyone can eat, trade, and
    /// covet, so an unseeded stream here desynchronises the whole run.</summary>
    public WorkAction(WorkSpec spec, Func<string, IItem> produceItem, Random? rng = null)
    {
        _spec = spec;
        _produceItem = produceItem;
        _rng = rng ?? new Random();
        Keywords = [.. spec.Keywords];
    }

    public string ActionName => "Work";
    public HashSet<string> Keywords { get; }
    public bool RequiresItem => false;

    public int Duration => Math.Max(1, _spec.ShiftBeats);

    /// <summary>Workers down tools once their stock of produced goods is full.</summary>
    public bool CanActorUse(Entity user) =>
        user.Inventory.Count(i => _spec.Produces.Contains(i.ItemId)) < _spec.MaxStock;

    public bool CanTarget(Entity user, Entity target) => user.ID == target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    /// <summary>
    /// One beat of the shift. The commute is part of the job — but it is not
    /// part of the WORK, so those beats elapse without banking anything and the
    /// shift pays out short. A worker who lives across the settlement really is
    /// worse off than one who lives above the kitchen, and nothing had to say so.
    /// </summary>
    public bool Sustain(Entity user, Entity target, IItem? item)
    {
        if (!_spec.Workplace.Contains(user.Position))
        {
            var center = (_spec.Workplace.Min + _spec.Workplace.Max) / 2f;
            center.Z = user.Position.Z;
            var delta = center - user.Position;
            float dist = delta.Length();
            if (dist > 0.01f)
                user.Position += Vector3.Normalize(delta) * Math.Min(EntityService.MoveSpeed(user), dist);
            return false;
        }

        user.Needs.AdjustMood(MoodPerBeat);
        return true;
    }

    /// <summary>
    /// Clock off and take the goods. Yield is the shift's total scaled by how
    /// much of it was actually worked, so time really is what decides the pay.
    /// </summary>
    /// <remarks>
    /// Rounded rather than floored: half a shift is worth half a shift's goods,
    /// and a run that banked almost nothing is worth nothing at all — walking
    /// out on the first beat has to be able to come to zero, or abandoning would
    /// be free.
    /// </remarks>
    public void Resolve(Entity user, Entity target, IItem? item, double performance)
    {
        int yield = (int)Math.Round(Math.Clamp(performance, 0, 1) * _spec.ShiftYield);
        for (int i = 0; i < yield; i++)
            user.Inventory.Add(_produceItem(_spec.Produces[_rng.Next(_spec.Produces.Count)]));
    }

    /// <summary>The whole job done at once — what a shift is worth at full marks.</summary>
    public void Execute(Entity user, Entity target, IItem? item) => Resolve(user, target, item, 1.0);
}
