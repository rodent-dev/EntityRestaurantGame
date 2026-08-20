using System.Numerics;
using Engine.Spatial;
using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>What a job is: where it happens, what it makes, when to stop.</summary>
public sealed record WorkSpec(
    Location Workplace,
    IReadOnlyList<string> Produces,
    int MaxStock,
    IReadOnlyList<string> Keywords);

/// <summary>
/// A job as an action CANDIDATE — it competes in the same weighted selection
/// as everything else, so working is a choice, never an assignment. Keywords
/// come from the work spec (jobs suit different natures: a cook's work reads
/// Industrious/Pride/Greedy, a patrol would read Industrious/Defensive).
/// Choosing work while away from the workplace spends the beat walking there;
/// on station, it produces one item from the spec's list.
/// </summary>
public sealed class WorkAction : IAction
{
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

    /// <summary>Workers down tools once their stock of produced goods is full.</summary>
    public bool CanActorUse(Entity user) =>
        user.Inventory.Count(i => _spec.Produces.Contains(i.ItemId)) < _spec.MaxStock;

    public bool CanTarget(Entity user, Entity target) => user.ID == target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    public void Execute(Entity user, Entity target, IItem? item)
    {
        if (!_spec.Workplace.Contains(user.Position))
        {
            // The commute is part of the job: step toward the workplace center.
            var center = (_spec.Workplace.Min + _spec.Workplace.Max) / 2f;
            center.Z = user.Position.Z;
            var delta = center - user.Position;
            float dist = delta.Length();
            if (dist > 0.01f)
                user.Position += Vector3.Normalize(delta) * Math.Min(EntityService.MoveSpeed(user), dist);
            return;
        }

        user.Inventory.Add(_produceItem(_spec.Produces[_rng.Next(_spec.Produces.Count)]));
    }
}
