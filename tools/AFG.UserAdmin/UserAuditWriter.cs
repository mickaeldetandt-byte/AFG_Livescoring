namespace AFG.UserAdmin;

public static class UserAuditWriter
{
    public static void Write(TextWriter writer, IReadOnlyCollection<UserAuditRow> users)
    {
        writer.WriteLine(
            "ID\tEmail\tRole\tIsActive\tPasswordResetRequired\tClub\tPlayer");

        foreach (var user in users)
        {
            var club = user.ClubId.HasValue
                ? $"{user.ClubId}: {user.ClubName ?? "(sans nom)"}"
                : "-";
            var player = user.PlayerId.HasValue
                ? $"{user.PlayerId}: {user.PlayerName ?? "(sans nom)"}"
                : "-";

            writer.WriteLine(
                $"{user.Id}\t{user.Email}\t{user.Role}\t{user.IsActive}\t" +
                $"{user.PasswordResetRequired}\t{club}\t{player}");
        }

        writer.WriteLine($"Total : {users.Count}");
    }
}
