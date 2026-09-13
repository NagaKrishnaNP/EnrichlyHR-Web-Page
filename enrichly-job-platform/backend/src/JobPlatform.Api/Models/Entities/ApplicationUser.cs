using Microsoft.AspNetCore.Identity;

namespace JobPlatform.Api.Models.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
