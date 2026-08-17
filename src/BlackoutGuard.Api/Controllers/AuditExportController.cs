using System.Security.Claims;
using System.Text;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin,Operator")]
public class AuditExportController : ControllerBase
{
    private readonly IAuditExportRepository _auditExportRepository;

    public AuditExportController(IAuditExportRepository auditExportRepository)
    {
        _auditExportRepository = auditExportRepository;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string format,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var fromUtc = from?.ToUniversalTime();
        var toUtc = to?.ToUniversalTime();

        var entries = await _auditExportRepository.GetAuditEntriesAsync(
            facilityId.Value, fromUtc, toUtc, ct);

        return format.ToLowerInvariant() switch
        {
            "csv" => ExportCsv(entries),
            "pdf" => ExportPdf(entries),
            _ => BadRequest(new { error = "Unsupported format. Use 'csv' or 'pdf'." })
        };
    }

    private IActionResult ExportCsv(IReadOnlyList<AuditExportEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Event,Rationale,Affected Load");

        foreach (var entry in entries)
        {
            var affected = entry.AffectedLoadName is not null
                ? $"{entry.AffectedLoadName} (relay {entry.AffectedLoadRelayAddress})"
                : string.Empty;

            sb.AppendLine(string.Join(",",
                CsvEscape(entry.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
                CsvEscape(entry.EventType),
                CsvEscape(entry.Rationale),
                CsvEscape(affected)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private IActionResult ExportPdf(IReadOnlyList<AuditExportEntry> entries)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header()
                    .Text("BlackoutGuard — Decision Audit Log")
                    .FontSize(16)
                    .Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(5);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Timestamp");
                        header.Cell().Element(CellStyle).Text("Event");
                        header.Cell().Element(CellStyle).Text("Rationale");
                        header.Cell().Element(CellStyle).Text("Affected Load");
                    });

                    foreach (var entry in entries)
                    {
                        table.Cell().Element(CellStyle).Text(
                            entry.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                        table.Cell().Element(CellStyle).Text(entry.EventType);
                        table.Cell().Element(CellStyle).Text(entry.Rationale);
                        table.Cell().Element(CellStyle).Text(
                            entry.AffectedLoadName is not null
                                ? $"{entry.AffectedLoadName} (relay {entry.AffectedLoadRelayAddress})"
                                : string.Empty);
                    }
                });
            });
        });

        return File(document.GeneratePdf(), "application/pdf",
            $"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4).PaddingHorizontal(4);

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue("facility_id");
        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }
}
