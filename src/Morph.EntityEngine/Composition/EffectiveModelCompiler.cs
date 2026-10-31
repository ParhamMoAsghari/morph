using System.Security.Cryptography;
using System.Text;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;
using Morph.EntityEngine.Validation;

namespace Morph.EntityEngine.Composition;

/// <summary>
/// Compiles a definition version into a deterministic flattened <see cref="EffectiveModel"/>.
/// Uses exact version pins only — never the environment's current pointer.
/// </summary>
public sealed class EffectiveModelCompiler
{
    private readonly IDefinitionStore _store;
    private readonly DefinitionGraphValidator _validator;
    private readonly IClock _clock;

    public EffectiveModelCompiler(
        IDefinitionStore store,
        DefinitionGraphValidator validator,
        IClock clock)
    {
        _store = store;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<EffectiveModel>> CompileAsync(
        DefinitionVersion root,
        CancellationToken cancellationToken = default)
    {
        var issues = await _validator.ValidateAsync(root, cancellationToken).ConfigureAwait(false);
        if (issues.Count > 0)
        {
            var message = string.Join("; ", issues.Select(i => $"{i.Code}: {i.Message}"));
            return Result.Failure<EffectiveModel>(Error.Validation(message));
        }

        var context = new CompileContext(root);
        var compileIssues = new List<DefinitionValidationIssue>();

        await FlattenAsync(root, pathPrefix: null, includeMode: null, depth: 0, context, compileIssues, cancellationToken)
            .ConfigureAwait(false);

        if (compileIssues.Count > 0)
        {
            var message = string.Join("; ", compileIssues.Select(i => $"{i.Code}: {i.Message}"));
            return Result.Failure<EffectiveModel>(Error.Validation(message));
        }

        var fields = context.FieldsById.Values
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.FieldId)
            .Select(f => new EffectiveField
            {
                FieldId = f.FieldId,
                Path = f.Path,
                Slug = f.Slug,
                DisplayName = f.DisplayName,
                DataType = f.DataType,
                IsRequired = f.IsRequired,
                IsIndexed = f.IsIndexed,
                IsUnique = f.IsUnique,
                Encrypt = f.Encrypt,
                SourceDefinitionId = f.SourceDefinitionId,
                SourceVersionId = f.SourceVersionId,
                IncludeMode = f.IncludeMode,
                SourcePaths = f.SourcePaths.OrderBy(p => p, StringComparer.Ordinal).ToArray()
            })
            .ToArray();

        // Unique FieldIds already enforced via FieldsById; also ensure unique paths.
        var pathGroups = fields.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        if (pathGroups.Count > 0)
        {
            var paths = string.Join(", ", pathGroups.Select(g => g.Key));
            return Result.Failure<EffectiveModel>(Error.Validation(
                $"Effective model has duplicate paths after conflict resolution: {paths}."));
        }

        var components = root.ComponentUses
            .OrderBy(c => c.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(c => new EffectiveComponent
            {
                ComponentUseId = c.ComponentUseId,
                Alias = c.Alias,
                TargetDefinitionId = c.TargetDefinitionId,
                PinnedVersionId = c.PinnedVersionId,
                Mode = c.Mode,
                IsRequired = c.IsRequired
            })
            .ToArray();

        var relationships = root.Relationships
            .OrderBy(r => r.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(r => new EffectiveRelationship
            {
                RelationshipId = r.RelationshipId,
                Slug = r.Slug,
                TargetDefinitionId = r.TargetDefinitionId,
                PinnedVersionId = r.PinnedVersionId,
                Cardinality = r.Cardinality,
                Mode = r.Mode
            })
            .ToArray();

        var pins = context.Pins.Values
            .OrderBy(p => p.Slug, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.VersionId)
            .ToArray();

        var compiledAt = _clock.UtcNow;
        var fingerprint = ComputeFingerprint(root, fields, components, relationships, pins);

        return Result.Success(new EffectiveModel
        {
            DefinitionId = root.DefinitionId,
            VersionId = root.VersionId,
            EnvironmentId = root.EnvironmentId,
            Slug = root.Slug,
            Kind = root.Kind,
            VersionNumber = root.VersionNumber,
            Fields = fields,
            Components = components,
            Relationships = relationships,
            PinnedDependencies = pins,
            CompiledAt = compiledAt,
            Fingerprint = fingerprint
        });
    }

    private async Task FlattenAsync(
        DefinitionVersion version,
        string? pathPrefix,
        ComponentIncludeMode? includeMode,
        int depth,
        CompileContext context,
        List<DefinitionValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (depth > EntityEngineLimits.MaxCompositionDepth)
        {
            issues.Add(new DefinitionValidationIssue(
                "composition_depth_exceeded",
                $"Composition depth exceeds {EntityEngineLimits.MaxCompositionDepth}.",
                pathPrefix));
            return;
        }

        if (pathPrefix is null)
        {
            // Root local fields
            foreach (var field in version.Fields.OrderBy(f => f.FieldId))
            {
                AddOrMergeField(context, field, field.Slug, includeMode: null, version, issues);
            }
        }
        else
        {
            foreach (var field in version.Fields.OrderBy(f => f.FieldId))
            {
                var path = $"{pathPrefix}.{field.Slug}";
                AddOrMergeField(context, field, path, includeMode, version, issues);
            }
        }

        foreach (var use in version.ComponentUses.OrderBy(u => u.Alias, StringComparer.OrdinalIgnoreCase))
        {
            var pinned = await _store.GetVersionAsync(use.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                issues.Add(new DefinitionValidationIssue(
                    "pinned_version_not_found",
                    $"Pinned version '{use.PinnedVersionId}' not found.",
                    use.Alias));
                continue;
            }

            context.Pins[pinned.DefinitionId] = new VersionPin(
                pinned.DefinitionId,
                pinned.VersionId,
                pinned.Slug,
                pinned.VersionNumber);

            var childPrefix = pathPrefix is null ? use.Alias : $"{pathPrefix}.{use.Alias}";

            if (use.Mode == ComponentIncludeMode.Reference)
            {
                // Reference: do not flatten child fields; expose a synthetic Guid slot keyed by alias.
                var refField = new FieldDefinition
                {
                    FieldId = DeriveReferenceFieldId(use.ComponentUseId),
                    Slug = use.Alias,
                    DisplayName = use.Alias,
                    DataType = FieldDataType.Guid,
                    IsRequired = use.IsRequired
                };
                AddOrMergeField(context, refField, childPrefix, ComponentIncludeMode.Reference, version, issues);
                continue;
            }

            await FlattenAsync(pinned, childPrefix, ComponentIncludeMode.Embed, depth + 1, context, issues, cancellationToken)
                .ConfigureAwait(false);
        }

        // Track relationship pins (do not flatten Reference targets).
        foreach (var rel in version.Relationships.OrderBy(r => r.Slug, StringComparer.OrdinalIgnoreCase))
        {
            var pinned = await _store.GetVersionAsync(rel.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                continue;
            }

            context.Pins[pinned.DefinitionId] = new VersionPin(
                pinned.DefinitionId,
                pinned.VersionId,
                pinned.Slug,
                pinned.VersionNumber);

            if (rel.Mode == ComponentIncludeMode.Embed && pathPrefix is null)
            {
                await FlattenAsync(
                        pinned,
                        rel.Slug,
                        ComponentIncludeMode.Embed,
                        depth + 1,
                        context,
                        issues,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static void AddOrMergeField(
        CompileContext context,
        FieldDefinition field,
        string path,
        ComponentIncludeMode? includeMode,
        DefinitionVersion source,
        List<DefinitionValidationIssue> issues)
    {
        var normalizedPath = DefinitionGraphValidator.NormalizePath(path);

        if (context.FieldsById.TryGetValue(field.FieldId, out var existingById))
        {
            // Same FieldId via diamond — identity merge; keep lexicographically smallest path as canonical.
            existingById.SourcePaths.Add(normalizedPath);
            if (string.CompareOrdinal(normalizedPath, existingById.Path) < 0)
            {
                existingById.Path = normalizedPath;
            }

            return;
        }

        if (context.PathOwners.TryGetValue(normalizedPath, out var existingFieldId)
            && existingFieldId != field.FieldId)
        {
            var resolution = context.Root.ConflictResolutions.FirstOrDefault(r =>
                string.Equals(
                    DefinitionGraphValidator.NormalizePath(r.ConflictPath),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));

            if (resolution is null)
            {
                issues.Add(new DefinitionValidationIssue(
                    "diamond_conflict",
                    $"Path '{normalizedPath}' is claimed by FieldIds '{existingFieldId}' and '{field.FieldId}'. Provide an explicit ConflictResolution.",
                    normalizedPath));
                return;
            }

            if (resolution.WinningFieldId == existingFieldId)
            {
                // Winner already present; ignore loser.
                return;
            }

            if (resolution.WinningFieldId == field.FieldId)
            {
                // Replace loser.
                context.FieldsById.Remove(existingFieldId);
                context.PathOwners[normalizedPath] = field.FieldId;
            }
            else
            {
                issues.Add(new DefinitionValidationIssue(
                    "conflict_resolution_mismatch",
                    $"ConflictResolution for '{normalizedPath}' WinningFieldId '{resolution.WinningFieldId}' matches neither claimant.",
                    normalizedPath));
                return;
            }
        }
        else
        {
            context.PathOwners[normalizedPath] = field.FieldId;
        }

        context.FieldsById[field.FieldId] = new MutableEffectiveField
        {
            FieldId = field.FieldId,
            Path = normalizedPath,
            Slug = field.Slug,
            DisplayName = field.DisplayName,
            DataType = field.DataType,
            IsRequired = field.IsRequired,
            IsIndexed = field.IsIndexed,
            IsUnique = field.IsUnique,
            Encrypt = field.Encrypt,
            SourceDefinitionId = source.DefinitionId,
            SourceVersionId = source.VersionId,
            IncludeMode = includeMode,
            SourcePaths = [normalizedPath]
        };
    }

    private static Guid DeriveReferenceFieldId(Guid componentUseId)
    {
        // Deterministic UUID derived from ComponentUseId so Reference slots are stable across compiles.
        var bytes = componentUseId.ToByteArray();
        bytes[0] ^= 0x52; // 'R'
        return new Guid(bytes);
    }

    private static string ComputeFingerprint(
        DefinitionVersion root,
        IReadOnlyList<EffectiveField> fields,
        IReadOnlyList<EffectiveComponent> components,
        IReadOnlyList<EffectiveRelationship> relationships,
        IReadOnlyList<VersionPin> pins)
    {
        var sb = new StringBuilder();
        sb.Append(root.DefinitionId).Append('|').Append(root.VersionId).Append('|').Append(root.Slug).Append('|')
            .Append((int)root.Kind).Append('|').Append(root.VersionNumber).Append('\n');

        foreach (var f in fields)
        {
            sb.Append("F|").Append(f.FieldId).Append('|').Append(f.Path).Append('|').Append(f.Slug).Append('|')
                .Append((int)f.DataType).Append('|').Append(f.IsRequired).Append('|')
                .Append(f.SourceDefinitionId).Append('|').Append(f.SourceVersionId).Append('|')
                .Append(f.IncludeMode?.ToString() ?? "-").Append('\n');
        }

        foreach (var c in components)
        {
            sb.Append("C|").Append(c.ComponentUseId).Append('|').Append(c.Alias).Append('|')
                .Append(c.TargetDefinitionId).Append('|').Append(c.PinnedVersionId).Append('|')
                .Append((int)c.Mode).Append('\n');
        }

        foreach (var r in relationships)
        {
            sb.Append("R|").Append(r.RelationshipId).Append('|').Append(r.Slug).Append('|')
                .Append(r.TargetDefinitionId).Append('|').Append(r.PinnedVersionId).Append('|')
                .Append((int)r.Cardinality).Append('|').Append((int)r.Mode).Append('\n');
        }

        foreach (var p in pins)
        {
            sb.Append("P|").Append(p.DefinitionId).Append('|').Append(p.VersionId).Append('|')
                .Append(p.VersionNumber).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class CompileContext
    {
        public CompileContext(DefinitionVersion root)
        {
            Root = root;
        }

        public DefinitionVersion Root { get; }

        public Dictionary<Guid, MutableEffectiveField> FieldsById { get; } = new();

        public Dictionary<string, Guid> PathOwners { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<Guid, VersionPin> Pins { get; } = new();
    }

    private sealed class MutableEffectiveField
    {
        public required Guid FieldId { get; init; }
        public required string Path { get; set; }
        public required string Slug { get; init; }
        public required string DisplayName { get; init; }
        public required FieldDataType DataType { get; init; }
        public bool IsRequired { get; init; }
        public bool IsIndexed { get; init; }
        public bool IsUnique { get; init; }
        public bool Encrypt { get; init; }
        public required Guid SourceDefinitionId { get; init; }
        public required Guid SourceVersionId { get; init; }
        public ComponentIncludeMode? IncludeMode { get; init; }
        public required List<string> SourcePaths { get; init; }
    }
}
