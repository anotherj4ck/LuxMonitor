namespace LuxMonitor.Models;

/// <summary>
/// Description d'un thème : où sont ses couleurs, et ce qu'il demande à Windows.
/// </summary>
/// <param name="Id">Identifiant stable, enregistré dans les réglages (ne jamais le renommer).</param>
/// <param name="DisplayName">Nom affiché dans le panneau des thèmes.</param>
/// <param name="ResourcePath">Fichier XAML qui contient les couleurs du thème.</param>
/// <param name="UseAcrylic">Demander à Windows le flou Acrylic derrière la fenêtre.</param>
/// <param name="IsDark">Thème sombre : Windows teinte alors son effet en sombre.</param>
/// <param name="PreviewBackground">Couleur de la pastille d'aperçu.</param>
/// <param name="PreviewAccent">Couleur d'accent de la pastille d'aperçu.</param>
public sealed record ThemeDefinition(
    string Id,
    string DisplayName,
    string ResourcePath,
    bool UseAcrylic,
    bool IsDark,
    string PreviewBackground,
    string PreviewAccent);
