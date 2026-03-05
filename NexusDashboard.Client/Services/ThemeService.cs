namespace NexusDashboard.Client.Services;

public class ThemeService
{
    public bool IsDark { get; private set; } = true;
    public event Action? OnThemeChanged;

    public void Toggle()
    {
        IsDark = !IsDark;
        OnThemeChanged?.Invoke();
    }
}
