using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuxMonitor.ViewModels;

/// <summary>
/// Base des ViewModels : prévient l'interface quand une valeur change,
/// pour que l'affichage se mette à jour tout seul (data binding WPF).
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
