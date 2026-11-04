namespace Sequence.Authorization;

public static class Permissions
{
    public static class Users
    {
        public const string Read   = "users.read";
        public const string Write  = "users.write";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string Read   = "roles.read";
        public const string Write  = "roles.write";
        public const string Delete = "roles.delete";
    }

    public static class Products
    {
        public const string Read   = "products.read";
        public const string Write  = "products.write";
        public const string Delete = "products.delete";
    }

    public static IEnumerable<string> All()
    {
        yield return Users.Read;
        yield return Users.Write;
        yield return Users.Delete;
        yield return Roles.Read;
        yield return Roles.Write;
        yield return Roles.Delete;
        yield return Products.Read;
        yield return Products.Write;
        yield return Products.Delete;
    }
}