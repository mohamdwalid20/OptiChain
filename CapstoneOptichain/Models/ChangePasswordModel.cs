using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class ChangePasswordModel
    {
        [Required]
        public string currentPassword { get; set; }
        
        [Required]
        public string newPassword { get; set; }
        
        [Required]
        public string confirmPassword { get; set; }
    }
}
