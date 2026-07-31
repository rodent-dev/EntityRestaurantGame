# Contract: UI Screen API

---

## IScreen

```csharp
public interface IScreen
{
    string Title { get; }
    void Render(ConsoleRenderer renderer);
    ScreenAction HandleInput(ConsoleKeyInfo key);
}
```

---

## ScreenAction

```csharp
public abstract class ScreenAction
{
    public static readonly ScreenAction Stay    = new StayAction();
    public static readonly ScreenAction Pop     = new PopAction();
    public static readonly ScreenAction Quit    = new QuitAction();

    public static ScreenAction Push(IScreen screen)    => new PushAction(screen);
    public static ScreenAction Replace(IScreen screen) => new ReplaceAction(screen);
}

// Concrete types (sealed, internal to UI assembly):
// StayAction, PopAction, QuitAction, PushAction(IScreen), ReplaceAction(IScreen)
```

---

## ScreenManager

```csharp
public class ScreenManager
{
    public ScreenManager(ConsoleRenderer renderer);

    public void Push(IScreen screen);
    public void Run();              // blocking; exits when stack is empty or Quit received
}
```

---

## MenuOption

```csharp
public class MenuOption
{
    public string Label { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public Func<ScreenAction> Execute { get; init; }
}
```

---

## ConsoleRenderer

```csharp
public class ConsoleRenderer
{
    public void Clear();
    public void DrawStatusBar(string text);
    public void DrawPanel(string title, IEnumerable<string> lines);
    public void DrawOptions(IList<MenuOption> options);
    public void Write(int x, int y, string text);
}
```

---

## Influence (domain addition)

```csharp
// Added to EntityObjects
public class Influence
{
    public string TargetActionName { get; init; }
    public int Strength { get; init; }
    public string Source { get; init; }     // "Player"
}

// Added to Entity
public Influence? PendingInfluence { get; set; }

// Added to ActionWeightSource enum
PlayerInfluence     // new value alongside Behaviors, Relationship, Status
```

---

## Key bindings (standard across all screens)

| Key | Action |
|-----|--------|
| `1`–`9` | Select numbered menu option |
| `Escape` | Pop current screen (Back) |
| `Space` | Advance turn (WorldScreen only) |
| `Q` | Quit (WorldScreen only) |

Screens that don't handle a given key return `ScreenAction.Stay`.

---

## Screen construction pattern

All screens accept their dependencies at construction. No screen holds a reference to
`ScreenManager` — they return `ScreenAction` values and let the manager handle navigation.

```csharp
// Good
return ScreenAction.Push(new EntityInspectScreen(entity, controller));

// Bad — screens must not call ScreenManager methods directly
_manager.Push(new EntityInspectScreen(...));
```
