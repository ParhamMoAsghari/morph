namespace Morph.Access.Domain;

public static class AccessActions
{
    public const string Create = "create";
    public const string Read = "read";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string List = "list";
    public const string Publish = "publish";
    public const string Migrate = "migrate";
    public const string FileDownload = "file.download";
    public const string FileUpload = "file.upload";
    public const string Subscribe = "subscribe";
    public const string SqlExecute = "sql.execute";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Create, Read, Update, Delete, List, Publish, Migrate, FileDownload, FileUpload, Subscribe, SqlExecute
    };
}
