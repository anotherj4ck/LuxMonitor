using System.Windows.Data;
using System.Windows.Markup;

namespace LuxMonitor.Localization;

/// <summary>
/// Raccourci XAML pour un texte traduit : Text="{loc:Tr Widget.Processor}".
/// Équivaut à {Binding [Widget.Processor], Source={x:Static loc:Loc.Instance}}, en plus lisible.
/// C'est une vraie liaison (Binding) : le texte change tout seul quand on change de langue.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
