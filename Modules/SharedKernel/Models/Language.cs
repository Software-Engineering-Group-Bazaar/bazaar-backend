using SharedKernel.Models;

namespace SharedKernel.Languages.Models
{
    public class Language
    {
        public int Id { get; set; }
        public Translation? Translation { get; set; }
        public required string Code { get; set; }
        public string? Name { get; set; }
    }
}
