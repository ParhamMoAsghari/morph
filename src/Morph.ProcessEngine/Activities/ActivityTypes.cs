namespace Morph.ProcessEngine.Activities;

public static class ActivityTypes
{
    public const string Sequence = "sequence";
    public const string If = "if";
    public const string SetVariable = "variables.set";
    public const string StringConcat = "string.concat";
    public const string StringReplace = "string.replace";
    public const string StringTrim = "string.trim";
    public const string JsonParse = "json.parse";
    public const string JsonStringify = "json.stringify";
    public const string JsonGet = "json.get";
    public const string JsonSet = "json.set";
    public const string JsonMerge = "json.merge";
    public const string HttpRequest = "http.request";
    public const string MorphDataCreate = "morph.data.create";
    public const string MorphDataGet = "morph.data.get";
    public const string MorphDataUpdate = "morph.data.update";
    public const string MorphDataDelete = "morph.data.delete";
    public const string MorphDataList = "morph.data.list";
    public const string Delay = "delay";
    public const string AccessAssert = "access.assert";
    public const string InvokeFunction = "morph.functions.invoke";

    public static readonly IReadOnlySet<string> BuiltIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Sequence, If, SetVariable,
        StringConcat, StringReplace, StringTrim,
        JsonParse, JsonStringify, JsonGet, JsonSet, JsonMerge,
        HttpRequest,
        MorphDataCreate, MorphDataGet, MorphDataUpdate, MorphDataDelete, MorphDataList,
        Delay, AccessAssert, InvokeFunction
    };
}
