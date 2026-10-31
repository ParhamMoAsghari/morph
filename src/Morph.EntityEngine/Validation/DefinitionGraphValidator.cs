using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Validation;

public sealed class DefinitionGraphValidator
{
    private readonly IDefinitionStore _store;

    public DefinitionGraphValidator(IDefinitionStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<DefinitionValidationIssue>> ValidateAsync(
        DefinitionVersion version,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<DefinitionValidationIssue>();

        ValidateLocalShape(version, issues);

        if (issues.Count > 0)
        {
            return issues;
        }

        await ValidateCompositionGraphAsync(version, issues, cancellationToken).ConfigureAwait(false);
        return issues;
    }

    private static void ValidateLocalShape(DefinitionVersion version, List<DefinitionValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(version.Slug))
        {
            issues.Add(new DefinitionValidationIssue("slug_required", "Definition slug is required."));
        }

        var fieldIds = new HashSet<Guid>();
        var fieldSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in version.Fields)
        {
            if (field.FieldId == Guid.Empty)
            {
                issues.Add(new DefinitionValidationIssue("field_id_required", "FieldId must be a non-empty UUID.", field.Slug));
            }
            else if (!fieldIds.Add(field.FieldId))
            {
                issues.Add(new DefinitionValidationIssue(
                    "duplicate_field_id",
                    $"Duplicate FieldId '{field.FieldId}'.",
                    field.Slug));
            }

            if (string.IsNullOrWhiteSpace(field.Slug))
            {
                issues.Add(new DefinitionValidationIssue("field_slug_required", "Field slug is required."));
            }
            else if (!fieldSlugs.Add(field.Slug.Trim()))
            {
                issues.Add(new DefinitionValidationIssue(
                    "duplicate_field_slug",
                    $"Duplicate field slug '{field.Slug}'.",
                    field.Slug));
            }
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var use in version.ComponentUses)
        {
            if (string.IsNullOrWhiteSpace(use.Alias))
            {
                issues.Add(new DefinitionValidationIssue("component_alias_required", "ComponentUse alias is required."));
            }
            else if (!aliases.Add(use.Alias.Trim()))
            {
                issues.Add(new DefinitionValidationIssue(
                    "duplicate_component_alias",
                    $"Duplicate ComponentUse alias '{use.Alias}'.",
                    use.Alias));
            }

            if (use.PinnedVersionId == Guid.Empty)
            {
                issues.Add(new DefinitionValidationIssue(
                    "version_pin_required",
                    $"ComponentUse '{use.Alias}' must pin an exact version id (no live inheritance).",
                    use.Alias));
            }

            if (use.TargetDefinitionId == Guid.Empty)
            {
                issues.Add(new DefinitionValidationIssue(
                    "component_target_required",
                    $"ComponentUse '{use.Alias}' requires TargetDefinitionId.",
                    use.Alias));
            }

            if (fieldSlugs.Contains(use.Alias.Trim()))
            {
                issues.Add(new DefinitionValidationIssue(
                    "alias_field_slug_collision",
                    $"ComponentUse alias '{use.Alias}' collides with a local field slug.",
                    use.Alias));
            }
        }

        var relSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in version.Relationships)
        {
            if (rel.PinnedVersionId == Guid.Empty)
            {
                issues.Add(new DefinitionValidationIssue(
                    "version_pin_required",
                    $"Relationship '{rel.Slug}' must pin an exact version id.",
                    rel.Slug));
            }

            if (string.IsNullOrWhiteSpace(rel.Slug))
            {
                issues.Add(new DefinitionValidationIssue("relationship_slug_required", "Relationship slug is required."));
            }
            else if (!relSlugs.Add(rel.Slug.Trim()))
            {
                issues.Add(new DefinitionValidationIssue(
                    "duplicate_relationship_slug",
                    $"Duplicate relationship slug '{rel.Slug}'.",
                    rel.Slug));
            }
        }

        var conflictPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resolution in version.ConflictResolutions)
        {
            if (string.IsNullOrWhiteSpace(resolution.ConflictPath))
            {
                issues.Add(new DefinitionValidationIssue("conflict_path_required", "ConflictResolution path is required."));
            }
            else if (!conflictPaths.Add(NormalizePath(resolution.ConflictPath)))
            {
                issues.Add(new DefinitionValidationIssue(
                    "duplicate_conflict_resolution",
                    $"Duplicate ConflictResolution for path '{resolution.ConflictPath}'.",
                    resolution.ConflictPath));
            }

            if (resolution.WinningFieldId == Guid.Empty)
            {
                issues.Add(new DefinitionValidationIssue(
                    "conflict_winner_required",
                    $"ConflictResolution for '{resolution.ConflictPath}' requires WinningFieldId.",
                    resolution.ConflictPath));
            }
        }
    }

    private async Task ValidateCompositionGraphAsync(
        DefinitionVersion root,
        List<DefinitionValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();

        await WalkAsync(root, path: root.Slug, depth: 0, visiting, visited, issues, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task WalkAsync(
        DefinitionVersion current,
        string path,
        int depth,
        HashSet<Guid> visiting,
        HashSet<Guid> visited,
        List<DefinitionValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!visiting.Add(current.DefinitionId))
        {
            issues.Add(new DefinitionValidationIssue(
                "composition_cycle",
                $"Composition cycle detected at '{path}' involving definition '{current.Slug}'.",
                path));
            return;
        }

        if (depth > EntityEngineLimits.MaxCompositionDepth)
        {
            issues.Add(new DefinitionValidationIssue(
                "composition_depth_exceeded",
                $"Composition depth exceeds maximum of {EntityEngineLimits.MaxCompositionDepth} at '{path}'.",
                path));
            visiting.Remove(current.DefinitionId);
            return;
        }

        foreach (var use in current.ComponentUses.OrderBy(u => u.Alias, StringComparer.OrdinalIgnoreCase))
        {
            var childPath = string.IsNullOrEmpty(path) ? use.Alias : $"{path}.{use.Alias}";
            var pinned = await _store.GetVersionAsync(use.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                issues.Add(new DefinitionValidationIssue(
                    "pinned_version_not_found",
                    $"Pinned version '{use.PinnedVersionId}' for component '{use.Alias}' was not found.",
                    childPath));
                continue;
            }

            if (pinned.DefinitionId != use.TargetDefinitionId)
            {
                issues.Add(new DefinitionValidationIssue(
                    "pinned_version_mismatch",
                    $"Pinned version '{use.PinnedVersionId}' does not belong to definition '{use.TargetDefinitionId}'.",
                    childPath));
                continue;
            }

            if (pinned.EnvironmentId != current.EnvironmentId)
            {
                issues.Add(new DefinitionValidationIssue(
                    "cross_environment_pin",
                    $"Component '{use.Alias}' pins a version from another environment.",
                    childPath));
                continue;
            }

            // Exact pin: do not follow "current" pointer — only the pinned version.
            await WalkAsync(pinned, childPath, depth + 1, visiting, visited, issues, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var rel in current.Relationships.OrderBy(r => r.Slug, StringComparer.OrdinalIgnoreCase))
        {
            if (rel.Mode != ComponentIncludeMode.Embed)
            {
                // Reference relationships do not deepen the embed composition graph for depth/cycle of fields,
                // but self-embed cycles via relationships still matter when Mode=Embed.
                var pinnedRel = await _store.GetVersionAsync(rel.PinnedVersionId, cancellationToken).ConfigureAwait(false);
                if (pinnedRel is null)
                {
                    issues.Add(new DefinitionValidationIssue(
                        "pinned_version_not_found",
                        $"Pinned version '{rel.PinnedVersionId}' for relationship '{rel.Slug}' was not found.",
                        rel.Slug));
                }
                else if (pinnedRel.DefinitionId != rel.TargetDefinitionId)
                {
                    issues.Add(new DefinitionValidationIssue(
                        "pinned_version_mismatch",
                        $"Relationship '{rel.Slug}' pin does not match target definition.",
                        rel.Slug));
                }

                continue;
            }

            var childPath = string.IsNullOrEmpty(path) ? rel.Slug : $"{path}.{rel.Slug}";
            var pinned = await _store.GetVersionAsync(rel.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                issues.Add(new DefinitionValidationIssue(
                    "pinned_version_not_found",
                    $"Pinned version '{rel.PinnedVersionId}' for relationship '{rel.Slug}' was not found.",
                    childPath));
                continue;
            }

            await WalkAsync(pinned, childPath, depth + 1, visiting, visited, issues, cancellationToken)
                .ConfigureAwait(false);
        }

        visiting.Remove(current.DefinitionId);
        visited.Add(current.DefinitionId);
    }

    internal static string NormalizePath(string path) =>
        string.Join('.', path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
