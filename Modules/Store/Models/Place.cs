namespace Store.Models
{
    public class Place
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int RegionId { get; set; } // Required foreign key property
        public Region Region { get; set; } = null!;
        public ICollection<StoreModel> Stores { get; } = new List<StoreModel>();
        public required string PostalCode { get; set; }
    }
}