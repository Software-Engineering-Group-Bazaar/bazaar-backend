namespace SharedKernel.Api.Dtos
{
    public class CreateLanguageRequestDto
    {
        public required string Code { get; set; }
        public string? Name { get; set; }
        // Client will send a JSON object (key-value pairs) for translations
        public required Dictionary<string, string> Translations { get; set; }
    }
}