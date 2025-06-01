namespace SharedKernel.Models
{
    public class Translation
    {
        public required int Id { get; set; }
        public required int LanguageId { get; set; }
        public Language Language { get; set; } = null!;
        public string Data { get; set; } = string.Empty;
    }
}
