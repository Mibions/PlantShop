using System.ComponentModel.DataAnnotations;

namespace PlantShop.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        public string Description { get; set; }

        public ICollection<Plant> Plants { get; set; }
    }
}