using System.Numerics;
using Engine.Render;
using Engine.Ui;
using Game.Contracts;
using Game.Services;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// The whisper wheel: category → verb → who → what with. Four rings, each one
/// narrowing the last, and the whisper fires when you reach the end of the
/// chain rather than partway down it.
/// </summary>
/// <remarks>
/// Rings one and two are navigation only. That is a deliberate choice about
/// what a whisper MEANS: one whisper is one specific act the narrator wanted,
/// not a mood. Rings that could fire on their own would make "Combat" and
/// "Attack Yorick with the knife" the same kind of object with different
/// breadth, and the standing order in the HUD would stop being readable as a
/// sentence.
/// <para>
/// A ring with nothing to choose is skipped rather than shown empty — a
/// self-targeted verb has no "who", and a verb with no items has no "what
/// with", so those chains are two or three steps and end where they run out.
/// The last ring always carries a "leave it to them" entry, so the player can
/// still decline to specify without the ring having to fire early.
/// </para>
/// <para>
/// Blocked verbs stay on the list, greyed, carrying the reason. See
/// <see cref="PlayerAffordances"/> for why hiding them is worse.
/// </para>
/// <para>
/// The verb rings are built ONCE per pawn, because the verb set never changes
/// after world build. Only the object rings are resolved on entry, since those
/// are the only part that churns.
/// </para>
/// <para>
/// Left-docked and non-blocking on purpose: the sim keeps deciding while you
/// choose, which is the point — a whisper is a bid, not an order.
/// </para>
/// </remarks>
public sealed class InfluenceScene : Scene
{
    private const float PanelW = 164f;

    private readonly EntityController _controller;

    /// <summary>Canvas rows something else owns along the bottom edge.</summary>
    private readonly float _bottomReserve;

    /// <summary>Ring one, cached — the verb set is fixed at world build.</summary>
    private List<(ActionCategory Category, List<Affordance> Verbs)>? _rings;
    private string? _ringsBuiltFor;

    private MenuList? _menu;

    // Where we are in the chain. Null means "not that deep yet".
    private ActionCategory? _category;
    private Affordance? _verb;
    private Entity? _target;
    private string? _blocked;      // reason shown when a greyed verb is confirmed

    public override bool BlocksSim => false;
    public override bool IsOverlay => true;

    /// <param name="bottomReserve">
    /// How much of the bottom edge is already spoken for — the party strip asks
    /// itself via <c>PartyHud.Footprint</c>. The list scrolls rather than
    /// growing into it, because two panels drawn over each other make both
    /// unreadable and the list is the one that can afford to lose rows.
    /// </param>
    public InfluenceScene(EntityController controller, float bottomReserve = 0f)
    {
        _controller = controller;
        _bottomReserve = bottomReserve;
    }

    protected override void OnLoad() => ShowCategories();

    // ── Ring 1: categories ───────────────────────────────────────────────

    private void ShowCategories()
    {
        if (_controller.FavoredEntity is not { } pawn) return;
        _category = null; _verb = null; _target = null; _blocked = null;

        if (_ringsBuiltFor != pawn.ID)
        {
            _rings = PlayerAffordances.ByCategory(pawn, _controller.ActiveEntities);
            _ringsBuiltFor = pawn.ID;
        }

        var items = _rings!
            .Select(ring => new MenuItem
            {
                // The count is the honest signal of whether a drawer is worth
                // opening: "Combat 3" tells you more than "Combat" ever could.
                Label = $"{ring.Category.Name}  {ring.Verbs.Count(v => v.Available)}",
                Execute = () => { ShowVerbs(ring.Category); return SceneAction.Stay; },
            })
            .ToList();

        items.Add(new MenuItem
        {
            Label = "(clear whisper)",
            Execute = () =>
            {
                pawn.PendingInfluence = null;
                return SceneAction.Pop;
            }
        });

        _menu = new MenuList(items);   // Draw sizes the window to the space
    }

    // ── Ring 2: verbs ────────────────────────────────────────────────────

    private void ShowVerbs(ActionCategory category)
    {
        _category = category; _verb = null; _target = null; _blocked = null;

        var verbs = _rings!.First(r => r.Category.Name == category.Name).Verbs;

        var items = verbs
            .Select(v => new MenuItem
            {
                Label = v.Action.ActionName,
                IsEnabled = v.Available,
                Execute = () => { ChooseVerb(v); return SceneAction.Stay; },
            })
            .ToList();

        _menu = new MenuList(items);   // Draw sizes the window to the space
    }

    // ── Ring 3: who ──────────────────────────────────────────────────────

    private void ChooseVerb(Affordance verb)
    {
        if (_controller.FavoredEntity is not { } pawn) return;
        _verb = verb;

        if (verb.Targets.Count == 0)
        {
            // Nobody to aim at — the verb is about its own user. Straight to
            // the item ring, or fire if there is nothing to carry either.
            ShowItemsOrFire(pawn, target: null);
            return;
        }

        var items = verb.Targets
            .Select(t => new MenuItem
            {
                Label = _controller.IsOurs(t) ? $"{t.DisplayName} *" : t.DisplayName,
                Execute = () => { _target = t; return ShowItemsOrFire(pawn, t); },
            })
            .ToList();

        if (verb.SelfUsable)
            items.Insert(0, new MenuItem
            {
                Label = "(no one in particular)",
                Execute = () => ShowItemsOrFire(pawn, target: null),
            });

        _menu = new MenuList(items);   // Draw sizes the window to the space
    }

    // ── Ring 4: what with ────────────────────────────────────────────────

    private SceneAction ShowItemsOrFire(Entity pawn, Entity? target)
    {
        var choices = PlayerAffordances.ItemChoices(pawn, _verb!.Action, target ?? pawn);

        // One choice is not a choice — asking the player to confirm the only
        // fist they own is a keystroke that buys nothing.
        if (choices.Count <= 1)
        {
            Whisper(pawn, _verb, target, choices.Count == 1 ? choices[0].ItemId : null);
            return SceneAction.Pop;
        }

        _target = target;
        var items = choices
            .Select(i => new MenuItem
            {
                Label = i.ItemName,
                Execute = () => { Whisper(pawn, _verb, target, i.ItemId); return SceneAction.Pop; },
            })
            .ToList();

        items.Insert(0, new MenuItem
        {
            Label = "(whatever comes to hand)",
            Execute = () => { Whisper(pawn, _verb, target, itemId: null); return SceneAction.Pop; },
        });

        _menu = new MenuList(items);   // Draw sizes the window to the space
        return SceneAction.Stay;
    }

    /// <summary>
    /// Set the standing order. The keywords come off the verb the player
    /// pointed at, so the sim never sees an action name — see
    /// <see cref="ActionTaxonomy.Answers"/>.
    /// </summary>
    private static void Whisper(Entity pawn, Affordance verb, Entity? target, string? itemId) =>
        pawn.PendingInfluence = new Influence
        {
            Keywords = [.. verb.Action.Keywords],
            TargetEntityId = target?.ID,
            ItemId = itemId,
            Strength = EntityController.InfluenceStrength,
            Source = "Player",
            Label = verb.Action.ActionName,
        };

    // ── Input ────────────────────────────────────────────────────────────

    public override SceneAction Update(double dt, in UiInput input)
    {
        if (_menu is null || _controller.FavoredEntity is not { IsAlive: true })
            return SceneAction.Pop;

        // Cancel walks back up one ring before it closes the menu, so a
        // misfired pick costs one keystroke rather than the whole interaction.
        if (input.Cancel)
        {
            if (_target is not null) { ChooseVerb(_verb!); return SceneAction.Stay; }
            if (_verb is not null) { ShowVerbs(_category!); return SceneAction.Stay; }
            if (_category is not null) { ShowCategories(); return SceneAction.Stay; }
            return SceneAction.Pop;
        }

        // Confirming a greyed verb explains itself instead of doing nothing.
        // A menu that ignores a keypress reads as broken; one that answers
        // reads as a world with a reason.
        if (input.Confirm && _category is not null && _verb is null)
        {
            var verbs = _rings!.First(r => r.Category.Name == _category.Name).Verbs;
            if (_menu.Selected < verbs.Count && !verbs[_menu.Selected].Available)
                _blocked = verbs[_menu.Selected].Reason;
        }
        else if (input.Up || input.Down)
        {
            _blocked = null;
        }

        return _menu.Handle(input);
    }

    // ── Draw ─────────────────────────────────────────────────────────────

    public override void Draw(Renderer2D r)
    {
        if (_menu is null || _controller.FavoredEntity is not { } pawn) return;

        var font = Ctx.Font;
        float line = font.LineHeight + 3;
        float pad = 6f;

        var title = new Vector4(0.92f, 0.85f, 0.70f, 1f);
        var dim = new Vector4(0.55f, 0.52f, 0.46f, 1f);
        var body = new Vector4(0.85f, 0.83f, 0.76f, 1f);
        var warn = new Vector4(0.85f, 0.55f, 0.40f, 1f);

        // Two lines of chrome, not five. At 480x270 a header per fact and a
        // footer per hint ate more than half the panel and squeezed the list
        // down to three scrolling rows — the one part anybody is here to read.
        float headerH = line * 2 + 4;
        float footerH = line;
        float budget = Ctx.Height - _bottomReserve - pad * 2 - headerH - footerH;

        // The list runs tighter than the header: menu rows are a scannable
        // column, not prose, and the two extra pixels per row cost a whole
        // entry at this canvas size.
        float rowH = font.LineHeight + 1;

        // Fit the list to what is left, counting the "..." markers as rows —
        // because they ARE rows. Reserving a fixed area of `room` rows and
        // sizing VisibleRows to leave space for both markers means the panel
        // is the same height whether or not the list happens to be scrolled,
        // so it never wobbles and never grows back into the party strip.
        int room = Math.Max(3, (int)(budget / rowH));
        bool scrolls = _menu.Count > room;
        _menu.VisibleRows = scrolls ? Math.Max(1, room - 2) : room;

        float listH = (scrolls ? room : _menu.Count) * rowH;
        float panelH = pad * 2 + headerH + listH + footerH;

        r.DrawRect(0, 0, PanelW, panelH, new Vector4(0.07f, 0.08f, 0.10f, 0.92f));
        r.DrawRectOutline(0, 0, PanelW, panelH, new Vector4(0.55f, 0.45f, 0.28f, 1f));

        float y = pad;
        r.DrawText(font, Breadcrumb(pawn), pad, y, title);
        y += line;

        // A gloomy pawn half-listens: influence lands scaled by mood. Paired
        // with the standing order on one line because they are the same
        // question — is anything of mine still in play, and will it be heard.
        int heeds = (int)(EntityController.InfluenceStrength * pawn.Needs.MoodRatio);
        r.DrawText(font, $"+{heeds}  {Standing(pawn)}", pad, y,
            pawn.PendingInfluence is null ? dim : body);
        y += line + 4;

        _menu.Draw(r, font, PanelW / 2f, y, rowH);

        // The reason a greyed row is greyed takes the hint's line rather than
        // its own, so the panel does not change height as it comes and goes.
        r.DrawText(font,
            _blocked ?? (_category is null ? "Esc closes" : "Esc goes back"),
            pad, panelH - line - 2, _blocked is not null ? warn : dim);
    }

    /// <summary>Where you are in the chain, as a sentence being assembled.</summary>
    private string Breadcrumb(Entity pawn)
    {
        if (_category is null) return $"Whisper: {pawn.DisplayName}";
        if (_verb is null) return $"{_category.Name}?";
        if (_target is null) return $"{_verb.Action.ActionName} at whom?";
        return $"{_verb.Action.ActionName} -> {_target.DisplayName}, with?";
    }

    /// <summary>The standing whisper, named with its object when it has one.</summary>
    private string Standing(Entity pawn)
    {
        if (pawn.PendingInfluence is not { } inf) return "standing: none";

        string who = inf.TargetEntityId is null
            ? ""
            : " -> " + (_controller.ActiveEntities
                .FirstOrDefault(e => e.ID == inf.TargetEntityId)?.DisplayName ?? "?");

        return $"standing: {inf.Label}{who}";
    }
}
