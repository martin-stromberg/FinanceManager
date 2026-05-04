---
name: razor-viewmodel
description: 'Implement or review the ViewModel pattern for Blazor Razor pages. Use when creating a new Razor page, adding logic to an existing page, or auditing whether a page correctly delegates all state and logic to a ViewModel. Triggers: "ViewModel erstellen", "Seite per ViewModel befüllen", "Logik kapseln", "neue Seite anlegen", "Razor page refactoring".'
---

# Razor Page → ViewModel Pattern

## Ziel

Jede Razor-Seite und jede wiederverwendbare Komponente mit eigener Logik kapselt ihren Zustand und ihre API-Aufrufe vollständig in einem ViewModel. Die Razor-Datei enthält nur UI-Binding, Guard-Checks und Event-Weiterleitung.

---

## Struktur im Projekt

```
FinanceManager.Web/
├── Components/Pages/<Bereich>/
│   └── MyPage.razor              # Nur UI + @code-Weiche
└── ViewModels/<Bereich>/
    ├── MyPageViewModel.cs        # Erbt BaseViewModel
    └── MyPageSomeTabViewModel.cs # Für Tabs / Sub-Komponenten
```

Namespace-Konvention: `FinanceManager.Web.ViewModels.<Bereich>`

---

## Basis-Klassen

| Anwendungsfall | Basisklasse |
|---|---|
| Einzelne Seite oder komplexe Komponente | `BaseViewModel` |
| Listenübersicht mit Paging / Suche | `BaseListViewModel<TItem>` |
| Detail-/Bearbeitungskarte | `BaseCardViewModel<TKeyValue>` |

Alle befinden sich in `FinanceManager.Web.ViewModels.Common`.

---

## ViewModel Checkliste

### 1. Klasse anlegen

```csharp
namespace FinanceManager.Web.ViewModels.<Bereich>;

public sealed class MyPageViewModel : BaseViewModel
{
    public MyPageViewModel(IServiceProvider services) : base(services) { }

    // Zustandseigenschaften (public, get; private set;)
    public bool Loading { get; private set; }
    public string? LastError { get; private set; }
    public MyDataDto? Data { get; private set; }

    public async Task LoadAsync(Guid id, CancellationToken ct = default)
    {
        if (!CheckAuthentication()) return;

        Loading = true;
        RaiseStateChanged();
        try
        {
            Data = await ApiClient.MyDomain_GetSomethingAsync(id, ct);
        }
        finally
        {
            Loading = false;
            RaiseStateChanged();
        }
    }
}
```

**Pflichtregeln:**
- Konstruktor nimmt nur `IServiceProvider services` entgegen.
- Alle öffentlichen Eigenschaften sind `get; private set;` (kein direktes Binding).
- Daten werden immer über `ApiClient.<Domain>_Xxx` geladen (kein direktes Service-Inject).
- `CheckAuthentication()` vor jedem API-Aufruf aufrufen.
- `Loading = true` + `RaiseStateChanged()` vor, `Loading = false` + `RaiseStateChanged()` nach dem API-Aufruf (im `finally`).
- `sealed` verwenden, wenn keine Ableitung geplant.

### 2. Razor-Seite verdrahten

```razor
@page "/my-route/{Id:guid}"
@rendermode InteractiveServer
@using FinanceManager.Web.ViewModels.<Bereich>
@inject IServiceProvider Services
@inject NavigationManager Nav

@if (_vm == null || _vm.Loading)
{
    <p>Lade...</p>
    return;
}
@if (!_vm.IsAuthenticated)
{
    <p>Bitte anmelden.</p>
    return;
}
@if (_vm.LastError != null)
{
    <p class="error">@_vm.LastError</p>
}

<!-- Inhalt -->

@code {
    [Parameter] public Guid Id { get; set; }

    private MyPageViewModel? _vm;

    protected override Task OnInitializedAsync()
    {
        _vm = new MyPageViewModel(Services);
        _vm.StateChanged += VmOnStateChanged;
        return Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_vm != null)
            await _vm.LoadAsync(Id);
    }

    public void Dispose()
    {
        if (_vm != null)
            _vm.StateChanged -= VmOnStateChanged;
    }

    private void VmOnStateChanged(object? sender, EventArgs e)
        => _ = InvokeAsync(StateHasChanged);
}
```

**Pflichtregeln:**
- `@rendermode InteractiveServer` in jeder Seite angeben.
- `@inject IServiceProvider Services` — kein direktes Injizieren von Services, die ins ViewModel gehören.
- Kein Geschäftslogik-Code im `@code`-Block außer Lifecycle-Weiterleitung.
- `_vm.StateChanged` immer in `Dispose()` abmelden.
- Guard-Checks (`_vm == null`, `!_vm.IsAuthenticated`, `_vm.NotFound`) am Seitenanfang.

---

## Tab-ViewModels (Sub-Komponenten)

Wenn eine Seite Tabs enthält, erhält jeder Tab eine eigene ViewModel-Klasse:

```csharp
public sealed class MyPageSomeTabViewModel : BaseViewModel
{
    public MyPageSomeTabViewModel(IServiceProvider services) : base(services) { }

    public async Task LoadAsync(Guid parentId, CancellationToken ct = default) { ... }
}
```

Der Tab-Razor-Komponente wird das ViewModel als `[Parameter]` übergeben:

```razor
@code {
    [Parameter] public MyPageSomeTabViewModel? Vm { get; set; }
}
```

Die Seite instanziiert die Tab-ViewModels und gibt sie weiter — oder jeder Tab erzeugt sich sein ViewModel selbst aus dem `IServiceProvider`:

```razor
@inject IServiceProvider Services
@code {
    [Parameter] public Guid ParentId { get; set; }
    private MyPageSomeTabViewModel? _vm;

    protected override Task OnInitializedAsync()
    {
        _vm = new MyPageSomeTabViewModel(Services);
        _vm.StateChanged += (_, _) => _ = InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_vm != null)
            await _vm.LoadAsync(ParentId);
    }
}
```

---

## Häufige Fehler

| Fehler | Korrekt |
|---|---|
| Services direkt in `.razor` injecten und API dort aufrufen | Services nur in `IServiceProvider` verpacken, ViewModel nutzt `ApiClient` |
| Zustand direkt im `@code`-Block als Felder | Felder gehören ins ViewModel |
| `StateHasChanged()` direkt aus ViewModel aufrufen | ViewModel feuert `RaiseStateChanged()`, Razor abonniert `StateChanged` |
| Kein `@rendermode InteractiveServer` | Immer explizit angeben |
| ViewModel in `OnAfterRenderAsync` statt `OnInitializedAsync` erstellen | Immer in `OnInitializedAsync` |
| `_vm.StateChanged` nicht in `Dispose()` abmelden | Memory Leak vermeiden |
| `async void` in Event-Handler (außer `EventCallback`) | `_ = InvokeAsync(...)` verwenden |

---

## Verzeichniskonvention für neue ViewModels

Lege ViewModels parallel zur Razor-Seitenstruktur ab:

```
Pages/Securities/SecurityDetailPage.razor
  → ViewModels/Securities/SecurityDetailPageViewModel.cs

Pages/Securities/Tabs/CashflowTab.razor
  → ViewModels/Securities/SecurityDetailCashflowTabViewModel.cs
```

---

## Prozess: Neue Seite mit ViewModel

1. **ViewModel-Klasse anlegen** unter `ViewModels/<Bereich>/`.
2. Alle Zustandseigenschaften, `LoadAsync`-Methoden und Business-Aktionen implementieren.
3. **Razor-Seite** anlegen unter `Components/Pages/<Bereich>/`.
4. Nur `IServiceProvider` injizieren, ViewModel in `OnInitializedAsync` instanziieren.
5. Guard-Checks am Seitenanfang.
6. `StateChanged` subscriben und in `Dispose()` abmelden.
7. Sicherstellen: **kein API-Aufruf, keine direkte Service-Nutzung** im `@code`-Block.
