using CommunityToolkit.Mvvm.ComponentModel;

namespace AgiDemo.ViewModels;

public partial class EventItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _eventType = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _icon = "\u2022";

    [ObservableProperty]
    private string _color = "#888";

    public static EventItemViewModel Create(string eventType, string content)
    {
        var (icon, color) = eventType switch
        {
            "thought" => ("\U0001F4AD", "#3498db"),
            "step" => ("\u25B6", "#2ecc71"),
            "question" => ("\u2753", "#f39c12"),
            "done" => ("\u2705", "#27ae60"),
            "error" => ("\u274C", "#e74c3c"),
            "log" => ("\U0001F4CB", "#95a5a6"),
            "paused" => ("\u23F8", "#f39c12"),
            "resumed" => ("\u25B6", "#2ecc71"),
            "user" => ("\U0001F464", "#9b59b6"),
            _ => ("\u2022", "#888"),
        };

        // Truncate long content
        if (content.Length > 300)
            content = content[..300] + "...";

        return new EventItemViewModel
        {
            EventType = eventType,
            Content = content,
            Icon = icon,
            Color = color,
        };
    }
}
