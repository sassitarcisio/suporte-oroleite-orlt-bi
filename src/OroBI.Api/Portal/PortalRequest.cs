using System.Globalization;
using OroBI.Application.Analytics;

namespace OroBI.Api.Portal;

internal sealed record PortalRequest(CommercialFilter Filter, int Year, int Month, int Page, int PageSize)
{
    public static bool TryRead(HttpRequest request, out PortalRequest parsed)
    {
        parsed = null!;
        var query = request.Query;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        if (query.ContainsKey("month") && !DateOnly.TryParseExact(query["month"], "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)) return false;
        if (month.Year is < 1900 or > 2100) return false;
        var start = month;
        var end = month.AddMonths(1).AddDays(-1);
        if (query.ContainsKey("startDate") && !DateOnly.TryParseExact(query["startDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) return false;
        if (query.ContainsKey("endDate") && !DateOnly.TryParseExact(query["endDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) return false;
        if (start > end || end.DayNumber - start.DayNumber > 3660 || start.Year < 1900 || end.Year > 2100) return false;
        if (!query.ContainsKey("month")) month = new(start.Year, start.Month, 1);
        var page = 1; var pageSize = 20;
        if (query.ContainsKey("page") && !int.TryParse(query["page"], out page)) return false;
        if (query.ContainsKey("pageSize") && !int.TryParse(query["pageSize"], out pageSize)) return false;
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) return false;
        foreach (var key in new[] { "brand", "city", "customerContains", "productContains" })
            if (query[key].ToString().Length > 200) return false;
        parsed = new(new CommercialFilter(start, end, Brand: query["brand"], City: query["city"],
            CustomerContains: query["customerContains"], ProductContains: query["productContains"]), month.Year, month.Month, page, pageSize);
        return true;
    }
}
