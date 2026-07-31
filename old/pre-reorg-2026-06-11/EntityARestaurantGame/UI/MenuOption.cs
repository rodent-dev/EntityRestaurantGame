namespace EntityARestaurantGame.UI;

public class MenuOption
{
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public Func<ScreenAction> Execute { get; init; } = () => ScreenAction.Stay;
}
