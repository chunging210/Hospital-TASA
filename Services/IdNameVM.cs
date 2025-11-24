namespace TASA.Services
{
    public record IdNameVM
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
