namespace OroBI.Application.Closings;

public interface ISellerClosingQueryService
{
    Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken);
    Task<ClosingConfigurationStatus> GetConfigurationStatusAsync(string seller, int year, int month, CancellationToken cancellationToken);
}

public sealed record ClosingConfigurationStatus(bool HasGoalValues, bool HasSalary, bool HasCommission, bool HasPppMaximumAward)
{
    public string ErrorMessage => !HasGoalValues
        ? "Nenhum arquivo VALOR_METAS concluido foi encontrado para configurar o fechamento."
        : MissingRequirements.Count == 0
            ? "Os valores do VALOR_METAS foram encontrados, mas o calculo nao gerou resultado para este vendedor e mes."
            : $"O fechamento esta sem: {string.Join(", ", MissingRequirements)}.";

    private IReadOnlyList<string> MissingRequirements =>
    [
        ..(HasSalary ? [] : new[] { "salario" }),
        ..(HasCommission ? [] : new[] { "comissao" }),
        ..(HasPppMaximumAward ? [] : new[] { "premio PPP" })
    ];
}
