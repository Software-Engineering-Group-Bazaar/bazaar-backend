namespace Store.Models
{
    public class Region // tumaci kao kanton
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Country { get; set; } = "ba";
        public ICollection<Place> Places { get; } = new List<Place>();
    }
}

// Regija 1->n Mjesta 1->n Store