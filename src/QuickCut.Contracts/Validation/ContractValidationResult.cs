namespace QuickCut.Contracts.Validation;

public sealed record ContractValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ContractValidationResult Valid() => new(true, []);

    public static ContractValidationResult Invalid(IReadOnlyList<string> errors) => new(false, errors);
}
