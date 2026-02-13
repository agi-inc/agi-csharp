using CommunityToolkit.Mvvm.ComponentModel;

namespace AgiDemo.ViewModels;

public partial class SessionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private string _agentName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public string DisplayName => $"{AgentName} ({Status})";
}
