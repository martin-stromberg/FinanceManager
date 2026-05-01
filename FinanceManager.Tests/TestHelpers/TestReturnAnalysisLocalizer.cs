using FinanceManager.Application.Securities.ReturnAnalysis;

namespace FinanceManager.Tests.TestHelpers;

/// <summary>
/// Stub implementation of <see cref="IReturnAnalysisLocalizer"/> for unit tests.
/// Returns the German (de) translation for every key so that existing string assertions
/// continue to work without modification. Unknown keys fall back to the key itself.
/// </summary>
public sealed class TestReturnAnalysisLocalizer : IReturnAnalysisLocalizer
{
    private static readonly Dictionary<string, string> _de = new()
    {
        // KPI DisplayNames
        ["Kpi_TotalReturn_DisplayName"]     = "Gesamtrendite",
        ["Kpi_MarketValue_DisplayName"]     = "Marktwert",
        ["Kpi_InvestedCapital_DisplayName"] = "Investiertes Kapital",
        ["Kpi_Cagr_DisplayName"]            = "CAGR p.a.",
        ["Kpi_Irr_DisplayName"]             = "IRR (pers.)",

        // KPI Formula texts
        ["Kpi_TotalReturn_Formula"]     = "Gesamtrendite (%) = (Marktwert + Verkaufserlöse (netto) + Netto-Dividenden − Investiertes Kapital) / Investiertes Kapital × 100",
        ["Kpi_MarketValue_Formula"]     = "Marktwert = Anzahl Anteile × Aktueller Kurs",
        ["Kpi_InvestedCapital_Formula"] = "Investiertes Kapital = Σ Kaufbeträge + Gebühren (FIFO-Kostenbasis der noch gehaltenen Anteile)",
        ["Kpi_Cagr_Formula"]            = "CAGR = ((Marktwert + Verkaufserlöse (netto) + Nettodividenden) / Investiertes Kapital) ^ (1 / Jahre) − 1",
        ["Kpi_Irr_Formula"]             = "Barwert = Cashflow / Diskontfaktor   |   Diskontfaktor = (1 + r)^t   |   IRR = r, sodass Σ Barwerte = 0",

        // KPI Descriptions
        ["Kpi_TotalReturn_Description"]     = "Zeigt die prozentuale Gesamtrendite des Wertpapiers. Die Aufschlüsselung unten zeigt, wie sich die absoluten Bestandteile zusammensetzen.",
        ["Kpi_MarketValue_Description"]     = "Der aktuelle Marktwert des Bestands, berechnet aus den gehaltenen Anteilen und dem zuletzt verfügbaren Kurs.",
        ["Kpi_InvestedCapital_Description"] = "Der Gesamtbetrag, der für die aktuell gehaltenen Anteile bezahlt wurde, inklusive aller Gebühren. Wird nach der FIFO-Methode berechnet: Verkäufe reduzieren zuerst die ältesten Kauflots.",
        ["Kpi_Cagr_Description"]            = "Durchschnittliche jährliche Wachstumsrate unter der Annahme gleichmäßigen Wachstums. Jahre = Zeitraum vom ersten Kauf bis heute in vollen Jahren.",
        ["Kpi_Irr_Description"]             = "Der Interne Zinsfuß (IRR) ist die persönliche Rendite p.a., die mit dem tatsächlichen Zeitpunkt jeder Ein- und Auszahlung gewichtet wird.\r\nEr gibt an: Hätte ich mein Geld stattdessen zu einem festen Zinssatz angelegt, welcher Zinssatz hätte exakt dasselbe Endergebnis geliefert? Je früher ein Kauf und je später eine Dividende oder der Verkauf, desto stärker wird die Rendite beeinflusst. Die berechnete Rate ist jener Wert r, bei dem die Summe aller abdiskontierten Cashflows (Barwerte) gleich null ergibt. Die Cashflows mit ihren Barwerten sind chronologisch unten aufgelistet.",

        // Group names
        ["Group_MarketValue"]      = "Marktwert",
        ["Group_CurrentMarketValue"] = "Aktueller Marktwert",
        ["Group_NetDividends"]     = "Nettodividenden (Dividenden − Steuern)",
        ["Group_DividendsNet"]     = "Dividenden (netto)",
        ["Group_InvestedCapital"]  = "Investiertes Kapital",
        ["Group_HoldingPeriod"]    = "Anlagedauer",
        ["Group_TotalReturn_Result"] = "Gesamtrendite",
        ["Group_Cagr_Result"]      = "CAGR (Ergebnis)",
        ["Group_IrrCashflows"]     = "Cashflows & Barwerte (XIRR-Berechnung)",
        ["Group_IrrProbe"]         = "Summe der Barwerte (Probe)",
        ["Group_Irr_Result"]       = "IRR (Ergebnis)",
        ["Group_Buys_WithShares"]  = "Käufe (Anteilszugänge)",
        ["Group_Sells_WithShares"] = "Verkäufe (Anteilsabgänge)",
        ["Group_Buys"]             = "Käufe",
        ["Group_Sells_Fifo"]       = "Verkäufe (FIFO-Anpassung)",
        ["Group_SalesProceeds_Net"] = "Verkaufserlöse (netto)",
        ["Group_RemainingFifoCostBasis"] = "FIFO-Kostenbasis (gehaltene Anteile)",

        // Group notes / dynamic texts
        ["Group_Irr_RateNote"]      = "r = {0} %  –  Die Rate, bei der die Summe aller Barwerte = 0 ergibt",
        ["Group_Irr_SignNote"]      = "Negativer Wert = Mittelabfluss (Kauf), Positiver Wert = Mittelzufluss (Dividende / Marktwert)",
        ["Group_IrrProbe_OK"]       = "Entspricht 0 – die IRR-Rate ist korrekt berechnet",
        ["Group_IrrProbe_Rounding"] = "Rundungsdifferenz: Die Abweichung von {0} EUR entsteht durch die auf 2 Dezimalstellen gerundeten Anzeigebeträge. Die interne Berechnung ergibt exakt 0.",

        // Item labels / notes
        ["Item_SharesTimesPrice"]        = "{0} Anteile × {1}",
        ["Item_BuySharesAtPrice"]        = "{0} Anteile à {1}",
        ["Item_Buy"]                     = "Kauf: {0} Stk. à {1}",
        ["Item_BuySimple"]               = "Kauf",
        ["Item_Fee"]                     = "Gebühr",
        ["Item_StandaloneFee"]           = "Gebühr (ohne Kaufzuordnung)",
        ["Item_Dividend_Net"]            = "Dividende (netto)",
        ["Item_Tax"]                     = "Steuer",
        ["Item_MarketValueTerminal"]     = "Aktueller Marktwert (fiktiver Abschluss)",
        ["Item_Sell"]                    = "Verkauf",
        ["Item_SellWithDetails"]         = "Verkauf: {0} Stk. à {1}",
        ["Item_FirstBuy"]                = "Erster Kauf",
        ["Item_Today"]                   = "Heute",
        ["Item_Period"]                  = "Zeitraum: {0}",
        ["Item_HoldingPeriodYearsMonths"] = "{0} Jahr(e) {1} Monat(e)",
        ["Item_AddShares"]               = "+{0} Anteile",
        ["Item_SubtractShares"]          = "−{0} Anteile",
        ["Item_Shares"]                  = "{0} Anteile",
        ["Item_TotalReturn_Note"]        = "= Marktwert + Verkaufserlöse (netto) − Investiertes Kapital + Netto-Dividenden",
        ["Item_PositionFullySold"]       = "Position vollständig verkauft",

        // Warnings
        ["Warning_NoPriceAvailable"] = "Kein aktueller Kurs verfügbar.",
        ["Warning_MissingPrices"]    = "Fehlende Kursdaten",
    };

    /// <inheritdoc/>
    public string this[string key] => _de.TryGetValue(key, out var v) ? v : key;

    /// <inheritdoc/>
    public string Format(string key, params object[] args)
    {
        var template = _de.TryGetValue(key, out var v) ? v : key;
        return string.Format(template, args);
    }
}
