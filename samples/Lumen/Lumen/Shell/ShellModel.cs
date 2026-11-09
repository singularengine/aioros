using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.Shell;

public sealed record ShellData(string Section, string Icon);

public sealed partial class ShellModel : ObservableObject
{
    public ShellModel(ShellData data) => Receive(data);

    [ObservableProperty] private string _section = "";
    [ObservableProperty] private string _icon = "";

    public void Receive(ShellData data) => (Section, Icon) = (data.Section, data.Icon);
}
