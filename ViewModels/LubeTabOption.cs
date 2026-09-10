using CommunityToolkit.Mvvm.ComponentModel;

namespace UmiaPP.ViewModels;

public partial class LubeTabOption : ObservableObject
{
    private readonly Action<LubeTabOption>? _changed;

    public string Key { get; }
    public string Title { get; }
    public bool CanHide { get; }

    [ObservableProperty]
    private bool _isVisible = true;

    public LubeTabOption(string key, string title, bool canHide, bool isVisible, Action<LubeTabOption>? changed = null)
    {
        Key = key;
        Title = title;
        CanHide = canHide;
        _isVisible = canHide ? isVisible : true;
        _changed = changed;
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!CanHide && !value)
        {
            IsVisible = true;
            return;
        }

        _changed?.Invoke(this);
    }
}
