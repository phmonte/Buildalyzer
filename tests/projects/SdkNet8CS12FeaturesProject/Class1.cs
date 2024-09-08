namespace SdkNet8CS12FeaturesProject
{
    // LangVersion12 Features https://github.com/phmonte/Buildalyzer/issues/281
    public class Class1
    {
        // https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#collection-expressions
        public void CollectionExpression()
        {
            // Create an array:
            int[] a = [1, 2, 3, 4, 5, 6, 7, 8];

            // Create a list:
            List<string> b = ["one", "two", "three"];

            // Create a span
            Span<char> c = ['a', 'b', 'c', 'd', 'e', 'f', 'h', 'i'];

            // Create a jagged 2D array:
            int[][] twoD = [[1, 2, 3], [4, 5, 6], [7, 8, 9]];

            // Create a jagged 2D array from variables:
            int[] row0 = [1, 2, 3];
            int[] row1 = [4, 5, 6];
            int[] row2 = [7, 8, 9];
            int[][] twoDFromVariables = [row0, row1, row2];

            int[] single = [.. row0, .. row1, .. row2];
            foreach (var element in single)
            {
                Console.Write($"{element}, ");
            }
        }

        public readonly struct Distance(double dx, double dy)
        {
            public readonly double Magnitude { get; } = Math.Sqrt(dx * dx + dy * dy);
            public readonly double Direction { get; } = Math.Atan2(dy, dx);
        }

        public struct Distance2(double dx, double dy)
        {
            public readonly double Magnitude => Math.Sqrt(dx * dx + dy * dy);
            public readonly double Direction => Math.Atan2(dy, dx);

            public void Translate(double deltaX, double deltaY)
            {
                dx += deltaX;
                dy += deltaY;
            }

            public Distance2() : this(0, 0) { }
        }

        public class BankAccount(string accountID, string owner)
        {
            public string AccountID { get; } = ValidAccountNumber(accountID)
                ? accountID
                : throw new ArgumentException("Invalid account number", nameof(accountID));

            public string Owner { get; } = string.IsNullOrWhiteSpace(owner)
                ? throw new ArgumentException("Owner name cannot be empty", nameof(owner))
                : owner;

            public override string ToString() => $"Account ID: {AccountID}, Owner: {Owner}";

            public static bool ValidAccountNumber(string accountID) =>
            accountID?.Length == 10 && accountID.All(c => char.IsDigit(c));
        }

        public class CheckingAccount(string accountID, string owner, decimal overdraftLimit = 0) : BankAccount(accountID, owner)
        {
            public decimal CurrentBalance { get; private set; } = 0;

            public void Deposit(decimal amount)
            {
                if (amount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive");
                }
                CurrentBalance += amount;
            }

            public void Withdrawal(decimal amount)
            {
                if (amount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive");
                }
                if (CurrentBalance - amount < -overdraftLimit)
                {
                    throw new InvalidOperationException("Insufficient funds for withdrawal");
                }
                CurrentBalance -= amount;
            }

            public override string ToString() => $"Account ID: {AccountID}, Owner: {Owner}, Balance: {CurrentBalance}";
        }
    }
}
