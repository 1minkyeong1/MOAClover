using System;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        public DateTime ExpireAt { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}
