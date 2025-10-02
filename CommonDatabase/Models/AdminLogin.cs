using System.ComponentModel.DataAnnotations;

namespace CommonDatabase.Models
{
    public class AdminLogin
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
        [MaxLength(50, ErrorMessage = "Username can't exceed 50 characters.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(4, ErrorMessage = "Password must be at least 4 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } 
        
    }
}
