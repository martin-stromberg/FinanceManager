namespace FinanceManager.Web.Services;

internal static class SecurityPriceProviderErrorUserMessageBuilder
{
    public static string Build(PriceProviderErrorClass errorClass, string securityName, string securityIdentifier, DateTime occurredUtc)
    {
        var occurredText = occurredUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return errorClass switch
        {
            PriceProviderErrorClass.InvalidSymbolOrFunction =>
                $"Für '{securityName}' ({securityIdentifier}) konnte kein Kurs geladen werden ({occurredText}). Bitte Symbol prüfen, speichern und anschließend den Abruf erneut starten.",
            PriceProviderErrorClass.UnknownProviderError =>
                $"Für '{securityName}' ({securityIdentifier}) ist beim Kursabruf ein externer Fehler aufgetreten ({occurredText}). Bitte Hinweis bestätigen und den Abruf später erneut starten.",
            _ =>
                $"Für '{securityName}' ({securityIdentifier}) ist beim Kursabruf ein Fehler aufgetreten ({occurredText})."
        };
    }
}
