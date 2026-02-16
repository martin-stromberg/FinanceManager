using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.Budget;
using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Minimal fallback implementation of <see cref="IBudgetReportExportService"/> used
    /// when no dedicated exporter is registered. Returns a small CSV stream.
    /// Production should replace this with a proper XLSX generator.
    /// </summary>
    internal sealed class BudgetReportExportService : IBudgetReportExportService
    {
        public Task<(string ContentType, string FileName, Stream Content)> GenerateXlsxAsync(Guid ownerUserId, BudgetReportExportRequest request, CancellationToken ct)
        {
            var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms, leaveOpen: true))
            {
                sw.WriteLine("AsOf,Months,DateBasis");
                sw.WriteLine($"{request.AsOfDate:yyyy-MM-dd},{request.Months},{request.DateBasis}");
                sw.Flush();
                ms.Position = 0;
            }

            return Task.FromResult(("text/csv", "budget-report.csv", (Stream)ms));
        }
    }
}
