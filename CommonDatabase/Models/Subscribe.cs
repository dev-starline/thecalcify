using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CommonDatabase.Models
{
    [Index(nameof(Identifier), IsUnique = true)]
    public class Subscribe
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Identifier is required.")]
        public string? Identifier { get; set; }

        [Required(ErrorMessage = "Contract is required.")]
        public string? Contract { get; set; }

        public bool IsActive { get; set; } = true;

        public int? Digit { get; set; }

        public string Type { get; set; }

        public DateTime? UpdateDate { get; set; } = DateTime.UtcNow;
    }
}
