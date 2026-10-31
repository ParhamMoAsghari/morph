using Morph.BuildingBlocks.Results;
using Morph.Plugins.Abstractions;
using Morph.ProcessEngine.Activities;
using Morph.ProcessEngine.Ir;

namespace Morph.ProcessEngine.Publishing;

public sealed class ProcessCompiler
{
    private readonly IProcessActivityRegistry? _pluginActivities;

    public ProcessCompiler(IProcessActivityRegistry? pluginActivities = null)
    {
        _pluginActivities = pluginActivities;
    }

    public Result<CompiledProcess> Compile(ProcessDocument document, Guid versionId)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(document.Slug))
        {
            errors.Add("Process slug is required.");
        }

        if (document.Steps.Count == 0)
        {
            errors.Add("Process must declare at least one step.");
        }

        ValidateSteps(document.Steps, errors);

        if (errors.Count > 0)
        {
            return Result.Failure<CompiledProcess>(Error.Validation(string.Join("; ", errors)));
        }

        var compiled = new CompiledProcess
        {
            Slug = document.Slug.Trim().ToLowerInvariant(),
            VersionId = versionId,
            Input = document.Input,
            Variables = document.Variables,
            Steps = document.Steps.Select(CompileStep).ToList()
        };

        return Result.Success(compiled);
    }

    private void ValidateSteps(IEnumerable<ProcessStepIr> steps, List<string> errors)
    {
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                errors.Add($"Step of type '{step.Type}' is missing id.");
            }

            if (string.IsNullOrWhiteSpace(step.Type))
            {
                errors.Add($"Step '{step.Id}' is missing type.");
                continue;
            }

            if (!ActivityTypes.BuiltIn.Contains(step.Type)
                && (_pluginActivities is null || !_pluginActivities.TryGet(step.Type, out _)))
            {
                errors.Add($"Unknown activity type '{step.Type}' on step '{step.Id}'.");
            }

            if (string.Equals(step.Type, ActivityTypes.If, StringComparison.OrdinalIgnoreCase))
            {
                if (step.Then is { Count: > 0 })
                {
                    ValidateSteps(step.Then, errors);
                }

                if (step.Else is { Count: > 0 })
                {
                    ValidateSteps(step.Else, errors);
                }
            }
            else if (string.Equals(step.Type, ActivityTypes.Sequence, StringComparison.OrdinalIgnoreCase)
                     && step.Steps is { Count: > 0 })
            {
                ValidateSteps(step.Steps, errors);
            }
        }
    }

    private static CompiledStep CompileStep(ProcessStepIr step) =>
        new()
        {
            Id = step.Id,
            Type = step.Type,
            With = step.With,
            Then = step.Then?.Select(CompileStep).ToList(),
            Else = step.Else?.Select(CompileStep).ToList(),
            Steps = step.Steps?.Select(CompileStep).ToList()
        };
}
