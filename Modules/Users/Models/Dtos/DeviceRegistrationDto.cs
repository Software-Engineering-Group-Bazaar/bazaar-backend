using System.ComponentModel.DataAnnotations;

namespace Users.Dtos
{
    public class DeviceRegistrationDto
    {
        [Required(ErrorMessage = "Device token is required.")]
        public required string DeviceToken { get; set; }
    }
}