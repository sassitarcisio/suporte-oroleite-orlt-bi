namespace OroBI.Api.Auth;

public static class AuthorizationPolicies
{
    public const string AdministratorOnly = "AdministratorOnly";
    public const string ManagerOrAdministrator = "ManagerOrAdministrator";
    public const string SellerScope = "SellerScope";
}
