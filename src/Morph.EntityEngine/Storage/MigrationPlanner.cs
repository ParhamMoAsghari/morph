using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Storage;

/// <summary>
/// Builds a staged <see cref="MigrationPlan"/> for create-table or additive evolve.
/// Stages: validate → expand → backfill → constrain → swap (→ drop obsolete).
/// </summary>
public sealed class MigrationPlanner
{
    private readonly IClock _clock;

    public MigrationPlanner(IClock clock)
    {
        _clock = clock;
    }

    public MigrationPlan PlanCreate(
        StorageMapping target,
        Guid? priorPublishedVersionId = null)
    {
        var steps = new List<MigrationStep>();
        var ordinal = 0;

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Validate,
            Kind = MigrationStepKind.NoOp,
            Description = $"Validate create plan for {target.QualifiedTableName}"
        });

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Expand,
            Kind = MigrationStepKind.EnsureSchema,
            Description = $"Ensure schema {target.SchemaName}",
            Sql = $"CREATE SCHEMA IF NOT EXISTS {PgIdent.QuoteIdent(target.SchemaName)};"
        });

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Expand,
            Kind = MigrationStepKind.CreateTable,
            Description = $"Create table {target.QualifiedTableName}",
            Sql = BuildCreateTableSql(target)
        });

        // Expand: non-unique indexes can be created early; unique deferred to constrain.
        foreach (var index in target.Indexes.Where(i => !i.IsUnique))
        {
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Expand,
                Kind = MigrationStepKind.AddIndex,
                Description = $"Create index {index.IndexName}",
                Sql = BuildCreateIndexSql(target, index)
            });
        }

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Backfill,
            Kind = MigrationStepKind.NoOp,
            Description = "Backfill skipped for empty create"
        });

        foreach (var index in target.Indexes.Where(i => i.IsUnique))
        {
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Constrain,
                Kind = MigrationStepKind.AddUnique,
                Description = $"Create unique index {index.IndexName}",
                Sql = BuildCreateIndexSql(target, index)
            });
        }

        foreach (var column in target.Columns.Where(c => c.DeferConstraints && !c.IsNullable))
        {
            // Create table already applied NOT NULL for non-deferred columns.
            // Deferred NOT NULL on create with no rows is applied as ALTER for stage fidelity.
            if (column.IsPrimaryKey || column.Kind == StorageColumnKind.System)
            {
                continue;
            }

            // Columns created nullable when DeferConstraints; tighten here.
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Constrain,
                Kind = MigrationStepKind.SetNotNull,
                Description = $"Set NOT NULL on {column.ColumnName}",
                Sql =
                    $"ALTER TABLE {target.QualifiedTableName} ALTER COLUMN {PgIdent.QuoteIdent(column.ColumnName)} SET NOT NULL;"
            });
        }

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Swap,
            Kind = MigrationStepKind.SwapPointer,
            Description = $"Swap published pointer to version {target.VersionId}"
        });

        return new MigrationPlan
        {
            PlanId = Guid.NewGuid(),
            EnvironmentId = target.EnvironmentId,
            DefinitionId = target.DefinitionId,
            FromVersionId = priorPublishedVersionId,
            ToVersionId = target.VersionId,
            TargetMapping = target,
            Steps = steps,
            RiskClass = MigrationRiskClass.Additive,
            CreatedAt = _clock.UtcNow
        };
    }

    public MigrationPlan PlanEvolve(StorageMapping from, StorageMapping to)
    {
        if (from.DefinitionId != to.DefinitionId)
        {
            throw new ArgumentException("Cannot evolve mappings across different definitions.");
        }

        if (!string.Equals(from.SchemaName, to.SchemaName, StringComparison.Ordinal)
            || !string.Equals(from.TableName, to.TableName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Schema/table rename is not supported in Phase 2 migrator; recreate instead.");
        }

        var steps = new List<MigrationStep>();
        var ordinal = 0;
        var risk = MigrationRiskClass.Additive;

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Validate,
            Kind = MigrationStepKind.NoOp,
            Description = $"Validate evolve {from.VersionId} → {to.VersionId}"
        });

        var fromCols = from.Columns.ToDictionary(c => c.ColumnName, StringComparer.Ordinal);
        var added = to.Columns.Where(c => !fromCols.ContainsKey(c.ColumnName)).ToList();

        foreach (var column in added)
        {
            // Expand: always add as nullable first.
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Expand,
                Kind = MigrationStepKind.AddColumn,
                Description = $"Add column {column.ColumnName}",
                Sql =
                    $"ALTER TABLE {to.QualifiedTableName} ADD COLUMN IF NOT EXISTS {PgIdent.QuoteIdent(column.ColumnName)} {column.PostgresType} NULL;"
            });

            if (column.Kind == StorageColumnKind.ExtrasJsonb)
            {
                steps.Add(new MigrationStep
                {
                    Ordinal = ordinal++,
                    Stage = MigrationStage.Backfill,
                    Kind = MigrationStepKind.BackfillColumn,
                    Description = $"Backfill {column.ColumnName} default '{{}}'",
                    Sql =
                        $"UPDATE {to.QualifiedTableName} SET {PgIdent.QuoteIdent(column.ColumnName)} = '{{}}'::jsonb WHERE {PgIdent.QuoteIdent(column.ColumnName)} IS NULL;"
                });
                risk = MigrationRiskClass.BackfillRequired;
            }
            else if (!column.IsNullable || column.DeferConstraints)
            {
                steps.Add(new MigrationStep
                {
                    Ordinal = ordinal++,
                    Stage = MigrationStage.Backfill,
                    Kind = MigrationStepKind.BackfillColumn,
                    Description = $"Backfill {column.ColumnName} (no-op placeholder)",
                    Sql = string.Empty
                });
                risk = MigrationRiskClass.BackfillRequired;
            }
        }

        var fromIndexes = from.Indexes.Select(i => i.IndexName).ToHashSet(StringComparer.Ordinal);
        foreach (var index in to.Indexes.Where(i => !fromIndexes.Contains(i.IndexName) && !i.IsUnique))
        {
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Expand,
                Kind = MigrationStepKind.AddIndex,
                Description = $"Create index {index.IndexName}",
                Sql = BuildCreateIndexSql(to, index)
            });
        }

        foreach (var column in added.Where(c => !c.IsNullable))
        {
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Constrain,
                Kind = MigrationStepKind.SetNotNull,
                Description = $"Set NOT NULL on {column.ColumnName}",
                Sql =
                    $"ALTER TABLE {to.QualifiedTableName} ALTER COLUMN {PgIdent.QuoteIdent(column.ColumnName)} SET NOT NULL;"
            });
        }

        foreach (var index in to.Indexes.Where(i => !fromIndexes.Contains(i.IndexName) && i.IsUnique))
        {
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.Constrain,
                Kind = MigrationStepKind.AddUnique,
                Description = $"Create unique index {index.IndexName}",
                Sql = BuildCreateIndexSql(to, index)
            });
        }

        var removed = from.Columns.Where(c => to.Columns.All(t => t.ColumnName != c.ColumnName)).ToList();
        foreach (var column in removed)
        {
            risk = MigrationRiskClass.Breaking;
            steps.Add(new MigrationStep
            {
                Ordinal = ordinal++,
                Stage = MigrationStage.DropObsolete,
                Kind = MigrationStepKind.DropColumn,
                Description = $"Drop obsolete column {column.ColumnName}",
                Sql =
                    $"ALTER TABLE {to.QualifiedTableName} DROP COLUMN IF EXISTS {PgIdent.QuoteIdent(column.ColumnName)};",
                IsDestructive = true
            });
        }

        steps.Add(new MigrationStep
        {
            Ordinal = ordinal++,
            Stage = MigrationStage.Swap,
            Kind = MigrationStepKind.SwapPointer,
            Description = $"Swap published pointer to version {to.VersionId}"
        });

        // Ensure swap comes before drop-obsolete in execution order by stage enum:
        // reorder so DropObsolete is after Swap.
        steps = steps
            .OrderBy(s => StageOrder(s.Stage))
            .ThenBy(s => s.Ordinal)
            .Select((s, i) => new MigrationStep
            {
                Ordinal = i,
                Stage = s.Stage,
                Kind = s.Kind,
                Description = s.Description,
                Sql = s.Sql,
                IsDestructive = s.IsDestructive
            })
            .ToList();

        return new MigrationPlan
        {
            PlanId = Guid.NewGuid(),
            EnvironmentId = to.EnvironmentId,
            DefinitionId = to.DefinitionId,
            FromVersionId = from.VersionId,
            ToVersionId = to.VersionId,
            TargetMapping = to,
            Steps = steps,
            RiskClass = risk,
            CreatedAt = _clock.UtcNow
        };
    }

    private static int StageOrder(MigrationStage stage) => stage switch
    {
        MigrationStage.Validate => 0,
        MigrationStage.Expand => 1,
        MigrationStage.Backfill => 2,
        MigrationStage.Constrain => 3,
        MigrationStage.Swap => 4,
        MigrationStage.DropObsolete => 5,
        _ => 99
    };

    private static string BuildCreateTableSql(StorageMapping mapping)
    {
        var defs = new List<string>();
        foreach (var column in mapping.Columns)
        {
            // Defer NOT NULL for constrained business columns so constrain stage is meaningful.
            var nullable = column.IsPrimaryKey || column.Kind == StorageColumnKind.System
                ? (column.IsNullable ? "NULL" : "NOT NULL")
                : (column.DeferConstraints ? "NULL" : (column.IsNullable ? "NULL" : "NOT NULL"));

            var defaultClause = column.Kind == StorageColumnKind.ExtrasJsonb
                ? " DEFAULT '{}'::jsonb"
                : column.ColumnName == "row_version"
                    ? " DEFAULT 0"
                    : string.Empty;

            defs.Add(
                $"{PgIdent.QuoteIdent(column.ColumnName)} {column.PostgresType} {nullable}{defaultClause}");
        }

        defs.Add($"PRIMARY KEY ({PgIdent.QuoteIdent("id")})");

        return
            $"CREATE TABLE IF NOT EXISTS {mapping.QualifiedTableName} ({string.Join(", ", defs)});";
    }

    private static string BuildCreateIndexSql(StorageMapping mapping, MappedIndex index)
    {
        var unique = index.IsUnique ? "UNIQUE " : string.Empty;
        var cols = string.Join(", ", index.ColumnNames.Select(PgIdent.QuoteIdent));
        return
            $"CREATE {unique}INDEX IF NOT EXISTS {PgIdent.QuoteIdent(index.IndexName)} ON {mapping.QualifiedTableName} ({cols});";
    }
}
