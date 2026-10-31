namespace Morph.ProcessEngine.Domain;

public enum ProcessVersionStatus
{
    Draft = 0,
    Published = 1,
    Deprecated = 2
}

public enum ProcessInstanceStatus
{
    Running = 0,
    Waiting = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
