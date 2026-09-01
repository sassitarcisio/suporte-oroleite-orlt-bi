using System.Text;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Abstractions;
using OroBI.Application.Imports;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Domain.Goals;
using OroBI.Domain.Ppp;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Imports;

public sealed class CsvImportWorkflow(OroBiDbContext dbContext, IImportFileStore fileStore) : IImportWorkflow
{
    public async Task<ImportExecutionResult> ImportAsync(ImportSubmission submission, CancellationToken cancellationToken)
    {
        await using var bufferedContent = new MemoryStream();
        await submission.Content.CopyToAsync(bufferedContent, cancellationToken);
        var bytes = bufferedContent.ToArray();

        await using var fileContent = new MemoryStream(bytes, writable: false);
        var storedFile = await fileStore.SaveAsync(fileContent, submission.FileName, submission.ContentType, cancellationToken);
        var validation = ImportCsvService.Validate(submission.FileType, DecodeCsv(bytes));
        var batch = ImportBatch.Start(submission.FileType, submission.FileName, storedFile.Sha256);

        dbContext.ImportBatches.Add(batch);
        if (validation.Status == ImportValidationStatus.Rejected)
        {
            foreach (var error in validation.Errors)
            {
                dbContext.ImportErrors.Add(ImportError.Create(batch.Id, 1, error.Message));
            }

            batch.Reject(storedFile.Uri, validation.Errors.Count);
        }
        else
        {
            var csv = DecodeCsv(bytes);
            var parsedPower = submission.FileType == ImportFileType.Power
                ? ParsePowerMovements(batch.Id, csv)
                : new PowerParseResult([], []);
            var parsedPpp = submission.FileType == ImportFileType.Ppp
                ? ParsePppRecords(batch.Id, csv)
                : new PppParseResult([], []);
            var parsedGoals = submission.FileType == ImportFileType.Goals
                ? ParseGoalRecords(batch.Id, csv)
                : new GoalParseResult([], []);
            var parsedGoalValues = submission.FileType == ImportFileType.GoalValues
                ? ParseGoalValueRecords(batch.Id, csv)
                : new GoalValueParseResult([], []);
            dbContext.CommercialMovements.AddRange(parsedPower.Movements);
            dbContext.PppRecords.AddRange(parsedPpp.Records);
            dbContext.GoalRecords.AddRange(parsedGoals.Records);
            dbContext.GoalValueRecords.AddRange(parsedGoalValues.Records);
            foreach (var error in parsedPower.Errors.Concat(parsedPpp.Errors).Concat(parsedGoals.Errors).Concat(parsedGoalValues.Errors))
            {
                dbContext.ImportErrors.Add(ImportError.Create(batch.Id, error.LineNumber, error.Message));
            }

            batch.Complete(storedFile.Uri, parsedPower.Movements.Count + parsedPpp.Records.Count + parsedGoals.Records.Count + parsedGoalValues.Records.Count, parsedPower.Errors.Count + parsedPpp.Errors.Count + parsedGoals.Errors.Count + parsedGoalValues.Errors.Count);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ImportExecutionResult(batch.Status, batch.StoredFileUri, batch.ProcessedRows, batch.ErrorRows);
    }

    private static string DecodeCsv(byte[] bytes)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static PowerParseResult ParsePowerMovements(Guid batchId, string csv)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new([], []);
        }

        var headers = lines[0].Split(';').Select((value, index) => new { Name = value.Trim().ToUpperInvariant(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var movements = new List<CommercialMovement>();
        var errors = new List<PowerRowError>();

        foreach (var item in lines.Skip(1).Select((line, index) => new { Line = line, LineNumber = index + 2 }))
        {
            try
            {
                var values = item.Line.Split(';');
                movements.Add(CommercialMovement.CreateFromImport(
                    batchId,
                    ParseDate(Value("DATA")),
                    Normalize(Value("VENDEDOR")),
                    Normalize(Value("MARCA")),
                    Normalize(Value("GRUPO")),
                    Normalize(Value("TIPO")),
                    Normalize(Value("CIDADE")),
                    Value("NOME").Trim(),
                    Value("PRODUTO").Trim(),
                    ParseDecimal("VALTOTAL", Value("VALTOTAL"), NumberStyles.Number | NumberStyles.AllowCurrencySymbol),
                    ParseDecimal("QTDE", Value("QTDE"), NumberStyles.Number),
                    ParseDecimal("PRECOCUSTO", Value("PRECOCUSTO"), NumberStyles.Number | NumberStyles.AllowCurrencySymbol),
                    Value("CODCLIENTE").Trim(),
                    Value("NRODOCUMENTO").Trim()));

                string Value(string header) => values[headers[header]].Trim();
            }
            catch (FormatException exception)
            {
                errors.Add(new(item.LineNumber, exception.Message));
            }
        }

        return new(movements, errors);

        DateOnly ParseDate(string value)
        {
            if (!DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new FormatException("DATA must use dd/MM/yyyy.");
            }

            return date;
        }

        decimal ParseDecimal(string header, string value, NumberStyles styles)
        {
            if (!decimal.TryParse(value, styles, culture, out var number))
            {
                throw new FormatException($"{header} must be a valid number.");
            }

            return number;
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static PppParseResult ParsePppRecords(Guid batchId, string csv)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new([], []);
        }

        var headers = lines[0].Split(';').Select((value, index) => new { Name = value.Trim().ToUpperInvariant(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
        var records = new List<PppRecord>();
        var errors = new List<PowerRowError>();
        foreach (var item in lines.Skip(1).Select((line, index) => new { Line = line, LineNumber = index + 2 }))
        {
            try
            {
                var values = item.Line.Split(';');
                records.Add(PppRecord.Create(
                    batchId,
                    ParseInteger("ANO"),
                    ParseInteger("MES"),
                    Normalize(Value("VENDEDOR")),
                    Normalize(Value("SEGMENTO")),
                    ParseInteger("QTDE_CLIENTES"),
                    ParseInteger("QTDE_ITENS_SEGMENTO"),
                    ParseInteger("GRUPOS_COLOCADOS")));

                string Value(string header) => values[headers[header]].Trim();
                int ParseInteger(string header) => int.TryParse(Value(header), CultureInfo.InvariantCulture, out var value)
                    ? value
                    : throw new FormatException($"{header} must be a valid integer.");
            }
            catch (FormatException exception)
            {
                errors.Add(new(item.LineNumber, exception.Message));
            }
        }

        return new(records, errors);
    }

    private static GoalParseResult ParseGoalRecords(Guid batchId, string csv)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return new([], []);
        var headers = lines[0].Split(';').Select((value, index) => new { Name = value.Trim().ToUpperInvariant(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
        var records = new List<GoalRecord>();
        var errors = new List<PowerRowError>();
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        foreach (var item in lines.Skip(1).Select((line, index) => new { Line = line, LineNumber = index + 2 }))
        {
            try
            {
                var values = item.Line.Split(';');
                var description = Value("DESCRICAO");
                records.Add(GoalRecord.Create(batchId, Normalize(Value("VENDEDOR")), Integer("MES"), Integer("ANO"), ClassifyGoalType(Value("TIPOMETA"), description), description, Decimal("META"), Decimal("ALCANCADO")));
                string Value(string header) => values[headers[header]].Trim();
                int Integer(string header) => int.TryParse(Value(header), CultureInfo.InvariantCulture, out var value) ? value : throw new FormatException($"{header} must be a valid integer.");
                decimal Decimal(string header) => decimal.TryParse(Value(header), NumberStyles.Number | NumberStyles.AllowCurrencySymbol, culture, out var value) ? value : throw new FormatException($"{header} must be a valid number.");
            }
            catch (FormatException exception) { errors.Add(new(item.LineNumber, exception.Message)); }
        }
        return new(records, errors);
    }

    private static string ClassifyGoalType(string rawType, string description)
    {
        if (description.Contains("POSITIV", StringComparison.OrdinalIgnoreCase)) return "POSITIVACAO";
        if (description.Contains("VALOR", StringComparison.OrdinalIgnoreCase) || description.Contains("FATUR", StringComparison.OrdinalIgnoreCase)) return "FATURAMENTO";
        if (description.Contains("TROCA", StringComparison.OrdinalIgnoreCase)) return "TROCA";
        return Normalize(rawType);
    }

    private static GoalValueParseResult ParseGoalValueRecords(Guid batchId, string csv)
    {
        const string header = "MARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL";
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var headerIndex = Array.FindIndex(lines, line => string.Equals(line.Trim().TrimStart('\uFEFF'), header, StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0 || headerIndex == lines.Length - 1) return new([], []);
        var records = new List<GoalValueRecord>();
        var errors = new List<PowerRowError>();
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        foreach (var item in lines.Skip(headerIndex + 1).Select((line, index) => new { Line = line, LineNumber = headerIndex + index + 2 }))
        {
            try
            {
                var values = item.Line.Split(';');
                if (values.Length != 5) throw new FormatException("Goal value row must contain five columns.");
                decimal Number(int index, string headerName)
                {
                    var raw = values[index].Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("%", string.Empty, StringComparison.Ordinal).Trim();
                    return decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, culture, out var value)
                        ? value
                        : throw new FormatException($"{headerName} must be a valid number.");
                }
                records.Add(GoalValueRecord.Create(batchId, Normalize(values[0]), Number(1, "FATURAMENTO"), Number(2, "POSITIVACAO"), Number(3, "TROCA"), Number(4, "TROCA_PERCENTUAL")));
            }
            catch (FormatException exception) { errors.Add(new(item.LineNumber, exception.Message)); }
        }
        return new(records, errors);
    }

    private sealed record PowerRowError(int LineNumber, string Message);

    private sealed record PowerParseResult(List<CommercialMovement> Movements, List<PowerRowError> Errors);

    private sealed record PppParseResult(List<PppRecord> Records, List<PowerRowError> Errors);

    private sealed record GoalParseResult(List<GoalRecord> Records, List<PowerRowError> Errors);

    private sealed record GoalValueParseResult(List<GoalValueRecord> Records, List<PowerRowError> Errors);
}
