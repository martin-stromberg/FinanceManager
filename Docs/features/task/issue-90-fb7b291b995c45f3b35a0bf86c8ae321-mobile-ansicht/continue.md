# Continue

- [x] Mobile Ribbon-Menüs auf Gruppen-Panels umstellen:
  - Jede Ribbon-Gruppe wird in der mobilen Ansicht als eigene Zeile dargestellt.
  - Jede Zeile wird als Panel mit abgerundeten Ecken gerendert.
  - Links im Panel steht der Gruppenname.
  - Rechts im Panel steht ein Hamburger-Menü-Button.
  - Das zugehörige Aufklappmenü zeigt die Aktionen der Gruppe jeweils mit Symbol und Namen.

- [x] Die Seite der Einstellungen mus verbessert werden. Jeder Inhaltsbereich soll sofort sichtbar sein. Es soll kein Auswahlpanel für die Anzeige der Bereiche mehr geben. Stattdessen soll jeder Inhaltsblock als zuklappbare registerkarte gezeigt werden, welche standardmäßig zugeklappt sind. Jede Registerkarte hat eine Titelleiste mit dem Namen linksbündig und dem Button für das Auf- und Zuklappen rechtsbündig.
- [x] Im Profil bei den Einstellungen ist die Checkbox für die Schklüsselfreigabe mobil nicht richtig dargestellt. Die Checkbox ist mittig, das Label dazu verschwindet am rechten Rand. Insgesamt scheinen mit dort die Checkboxen nicht gut platziert zu sein. Bei den Benachrichtigungen ist auch die Checkbox für den Monatsabschlusshinweis mittig. Der Text dazu passt zwar, aber insgesamt sollten diese Checkboxen eher linksbündig sein.
- [x] Bei den Einstellungen der Kontoauszüge fehlen die Übersetzungen für die Massenimportdialogeinstellungen.

- [x] Die Seite der Bankkontoübersicht ist nicht für mobile Ansichten optimiert. Das gilt grundsätzlich für alle Tabellenübersichten (Kontakte, Sparpläne, etc.) Bei der Desktopansicht ist das so in Ordnung, bei der mobilen Ansicht müssen wir "Stacked Cards" darstellen. Auch sollte stets überlegt, werden, ob eventuell Informationen in der mobilen Ansicht wegfallen können.
- [x] In der Tabell der Bankkonten sind die Werte für den "Typ" nicht übersetzt.

- [x] Gelegentlich tritt folgender Fehler auf, der dem Anwender mit voller Detailstärke präsentiert wird. System.Net.Http.HttpRequestException: Response status code does not indicate success: 500 (Internal Server Error).
   at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()
   at FinanceManager.Shared.ApiClient.EnsureSuccessOrSetErrorAsync(HttpResponseMessage resp) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Shared\ApiClient.cs:line 107
   at FinanceManager.Shared.ApiClient.Budgets_GetMonthlyKpiAsync(Nullable`1 date, BudgetReportDateBasis dateBasis, CancellationToken ct) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Shared\ApiClient.BudgetReport.cs:line 64
   at FinanceManager.Web.ViewModels.Budget.MonthlyBudgetKpiViewModel.LoadAsync(IApiClient api, CancellationToken ct) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Web\ViewModels\Budget\MonthlyBudgetKpiViewModel.cs:line 72
   at FinanceManager.Web.Components.Shared.MonthlyBudgetKpi.OnParametersSetAsync() in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Web\Components\Shared\MonthlyBudgetKpi.razor:line 75
   at Microsoft.AspNetCore.Components.ComponentBase.CallStateHasChangedOnAsyncCompletion(Task task)
   at Microsoft.AspNetCore.Components.RenderTree.Renderer.GetErrorHandledTask(Task taskToHandle, ComponentState owningComponentState)
   at Microsoft.AspNetCore.Components.RenderTree.Renderer.GetErrorHandledTask(Task taskToHandle, ComponentState owningComponentState)
   at Microsoft.AspNetCore.Components.Endpoints.EndpointHtmlRenderer.<WaitForNonStreamingPendingTasks>g__Execute|58_0()
   at Microsoft.AspNetCore.Components.Endpoints.EndpointHtmlRenderer.WaitForResultReady(Boolean waitForQuiescence, PrerenderedComponentHtmlContent result)
   at Microsoft.AspNetCore.Components.Endpoints.EndpointHtmlRenderer.RenderEndpointComponent(HttpContext httpContext, Type rootComponentType, ParameterView parameters, Boolean waitForQuiescence)
   at Microsoft.AspNetCore.Components.Endpoints.RazorComponentEndpointInvoker.RenderComponentCore(HttpContext context)
   at Microsoft.AspNetCore.Components.Endpoints.RazorComponentEndpointInvoker.RenderComponentCore(HttpContext context)
   at Microsoft.AspNetCore.Components.Rendering.RendererSynchronizationContext.<>c.<<InvokeAsync>b__10_0>d.MoveNext()
--- End of stack trace from previous location ---
   at Microsoft.AspNetCore.Builder.ServerRazorComponentsEndpointConventionBuilderExtensions.<>c__DisplayClass1_1.<<AddInteractiveServerRenderMode>b__1>d.MoveNext()
--- End of stack trace from previous location ---
   at FinanceManager.Web.Infrastructure.Auth.JwtRefreshMiddleware.InvokeAsync(HttpContext context) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Web\Infrastructure\Auth\JwtRefreshMiddleware.cs:line 121
   at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
   at Microsoft.AspNetCore.Authentication.AuthenticationMiddleware.Invoke(HttpContext context)
   at FinanceManager.Web.Infrastructure.IpBlockMiddleware.InvokeAsync(HttpContext context, AppDbContext db) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Web\Infrastructure\IpBlockMiddleware.cs:line 70
   at FinanceManager.Web.Infrastructure.RequestLoggingMiddleware.InvokeAsync(HttpContext context) in D:\Repositories\softwareschmiede\fb7b291b-995c-45f3-b35a-0bf86c8ae321\FinanceManager.Web\Infrastructure\RequestLoggingMiddleware.cs:line 45
   at Microsoft.AspNetCore.Localization.RequestLocalizationMiddleware.Invoke(HttpContext context)
   at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareImpl.Invoke(HttpContext context)
