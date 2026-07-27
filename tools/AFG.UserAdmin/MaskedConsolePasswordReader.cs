using System.Text;

namespace AFG.UserAdmin;

public sealed class MaskedConsolePasswordReader
{
    public string ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            throw new UserAdminException(
                "La saisie du mot de passe doit être interactive.");
        }

        Console.Write(prompt);
        var password = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (key.Key == ConsoleKey.C &&
                key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                Console.WriteLine();
                throw new UserAdminException("Opération annulée.");
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
