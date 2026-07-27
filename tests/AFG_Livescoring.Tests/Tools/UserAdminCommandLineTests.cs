using AFG.UserAdmin;
using Xunit;

namespace AFG_Livescoring.Tests.Tools;

public class UserAdminCommandLineTests
{
    [Fact]
    public void Parse_AcceptsAuditDryRunOnly()
    {
        var valid = UserAdminCommandLine.Parse(new[] { "audit", "--dry-run" });
        var withoutDryRun = UserAdminCommandLine.Parse(new[] { "audit" });

        Assert.True(valid.IsValid);
        Assert.Equal(UserAdminCommand.Audit, valid.Command);
        Assert.False(withoutDryRun.IsValid);
    }

    [Theory]
    [InlineData("--password")]
    [InlineData("--password=secret-value")]
    [InlineData("--PASSWORD=secret-value")]
    public void Parse_RejectsPasswordArguments(string passwordArgument)
    {
        var result = UserAdminCommandLine.Parse(
            new[]
            {
                "ensure-admin",
                "--email",
                "admin@example.invalid",
                passwordArgument
            });

        Assert.False(result.IsValid);
        Assert.Contains("jamais", result.Error);
    }
}
