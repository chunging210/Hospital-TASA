namespace TASA.Library.Core
{
    public class MailServiceEvent : EventArgs
    {
        public required string Addresses { get; init; }

        public required string ClassName { get; init; }

        public required string FunctionName { get; init; }

        public string? ExceptionMessage { get; init; }
    }
}
