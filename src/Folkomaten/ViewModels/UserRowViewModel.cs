using Folkomaten.Core;

namespace Folkomaten.ViewModels;

/// <summary>Én rad i listen: favorittstjerne, navn, fødselsnummer, fødselsdato og tilbakemelding ved kopiering.</summary>
internal sealed class UserRowViewModel(TestUser user, bool isFavorite) : ObservableObject
{
    private bool _isFavorite = isFavorite;
    private bool _isCopied;

    public TestUser User { get; } = user;

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (Set(ref _isFavorite, value))
            {
                Notify(nameof(FavoriteToolTip));
            }
        }
    }

    public string FavoriteToolTip => IsFavorite ? "Fjern favoritt" : "Legg til favoritt";

    /// <summary>True et øyeblikk etter at nummeret ble kopiert, så raden kan vise en hake.</summary>
    public bool IsCopied
    {
        get => _isCopied;
        set => Set(ref _isCopied, value);
    }
}
