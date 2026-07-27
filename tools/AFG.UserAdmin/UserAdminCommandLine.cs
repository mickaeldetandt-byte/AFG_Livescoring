namespace AFG.UserAdmin;

public enum UserAdminCommand
{
    None,
    Audit,
    EnsureAdmin
}

public sealed record ParsedUserAdminCommand(
    bool IsValid,
    UserAdminCommand Command,
    string? Email,
    string? Error)
{
    public static ParsedUserAdminCommand Invalid(string error) =>
        new(false, UserAdminCommand.None, null, error);
}

public static class UserAdminCommandLine
{
    public static ParsedUserAdminCommand Parse(string[] args)
    {
        if (args.Any(arg =>
                arg.Equals("--password", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("--password=", StringComparison.OrdinalIgnoreCase)))
        {
            return ParsedUserAdminCommand.Invalid(
                "Le mot de passe ne doit jamais être fourni dans les arguments.");
        }

        if (args.SequenceEqual(new[] { "audit", "--dry-run" }))
        {
            return new ParsedUserAdminCommand(
                true,
                UserAdminCommand.Audit,
                null,
                null);
        }

        if (args.Length == 3 &&
            args[0].Equals("ensure-admin", StringComparison.OrdinalIgnoreCase) &&
            args[1].Equals("--email", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(args[2]))
        {
            return new ParsedUserAdminCommand(
                true,
                UserAdminCommand.EnsureAdmin,
                args[2].Trim(),
                null);
        }

        return ParsedUserAdminCommand.Invalid("Commande ou arguments invalides.");
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Utilisation :");
        writer.WriteLine("  AFG.UserAdmin audit --dry-run");
        writer.WriteLine("  AFG.UserAdmin ensure-admin --email <email>");
    }
}
