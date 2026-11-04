namespace Sequence.Models;

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // e.g. "products.read"
    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}