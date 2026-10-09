using System.Collections.ObjectModel;
using Folkomaten.Core;
using Folkomaten.Platform;

namespace Folkomaten.ViewModels;

/// <summary>Tilstanden bak popupen: den filtrerte listen, filterfeltene og bunnteksten.</summary>
internal sealed class PopupViewModel : ObservableObject
{
    private const string NoUsersGlyph = "";
    private const string NoFavoritesGlyph = "";
    private const string NoMatchesGlyph = "";

    private readonly TestUserStore _store;
    private string _search = "";
    private bool _onlyFavorites;
    private bool _showDataActions;
    private bool _isFetchingFromTenor;

    public PopupViewModel(TestUserStore store)
    {
        _store = store;
        Refresh();
    }

    public ObservableCollection<UserRowViewModel> Rows { get; } = [];

    public string Search
    {
        get => _search;
        set
        {
            if (Set(ref _search, value))
            {
                Refresh();
            }
        }
    }

    public bool OnlyFavorites
    {
        get => _onlyFavorites;
        set
        {
            if (Set(ref _onlyFavorites, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowDataActions
    {
        get => _showDataActions;
        set => Set(ref _showDataActions, value);
    }

    public bool IsFetchingFromTenor
    {
        get => _isFetchingFromTenor;
        set
        {
            if (Set(ref _isFetchingFromTenor, value))
            {
                Notify(nameof(FetchLabel));
            }
        }
    }

    public string FetchLabel => IsFetchingFromTenor ? "Henter …" : "1. Hent fra Tenor";

    public bool StartAtLogin
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            StartupRegistration.SetEnabled(value);
            Notify();
        }
    }

    public bool HasRows => Rows.Count > 0;

    public int ResultCount => Rows.Count;

    public string SourceName => _store.SourceName;

    public bool CanLoadEmbedded => !_store.IsShowingEmbedded;

    public bool CanClear => _store.Users.Count > 0;

    public string EmptyStateGlyph =>
        _store.Users.Count == 0 ? NoUsersGlyph : OnlyFavorites ? NoFavoritesGlyph : NoMatchesGlyph;

    public string EmptyStateText =>
        _store.Users.Count == 0 ? "Ingen testbrukere. Last inn en fil eller hent nye nedenfor."
        : OnlyFavorites ? "Ingen favoritter ennå"
        : "Ingen treff";

    public void ToggleFavorite(UserRowViewModel row)
    {
        _store.ToggleFavorite(row.User);
        if (OnlyFavorites)
        {
            Refresh();
        }
        else
        {
            row.IsFavorite = _store.IsFavorite(row.User);
        }
    }

    public void LoadEmbedded()
    {
        _store.LoadEmbedded();
        Refresh();
    }

    public void LoadFile(string filePath)
    {
        _store.LoadFile(filePath);
        Refresh();
    }

    public void Clear()
    {
        _store.Clear();
        Refresh();
    }

    private void Refresh()
    {
        Rows.Clear();
        foreach (var user in _store.Filtered(Search, OnlyFavorites))
        {
            Rows.Add(new UserRowViewModel(user, _store.IsFavorite(user)));
        }

        Notify(nameof(HasRows));
        Notify(nameof(ResultCount));
        Notify(nameof(SourceName));
        Notify(nameof(CanLoadEmbedded));
        Notify(nameof(CanClear));
        Notify(nameof(EmptyStateGlyph));
        Notify(nameof(EmptyStateText));
    }
}
