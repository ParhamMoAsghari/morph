using Morph.Cli.Config;
using Morph.Cli.Schema;

namespace Morph.Cli.Commands;

internal static class InitCommand
{
    public static int Run(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var name = ArgUtil.GetOption(args, "--name") ?? new DirectoryInfo(root).Name;
        var force = ArgUtil.HasFlag(args, "--force");

        var morphYaml = Path.Combine(root, ProjectPaths.MorphYaml);
        if (File.Exists(morphYaml) && !force)
        {
            Console.Error.WriteLine($"'{ProjectPaths.MorphYaml}' already exists. Use --force to overwrite.");
            return 1;
        }

        var config = new ProjectConfig
        {
            Name = name,
            Version = "0.1.0",
            ApiUrl = ArgUtil.GetOption(args, "--api") ?? "http://localhost:5100",
            Codegen = new CodegenConfig
            {
                Typescript = "src/generated",
                Csharp = "Generated"
            }
        };
        ProjectConfigStore.Save(root, config);

        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.DefinitionsDir));
        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.PoliciesDir));
        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.DotMorphDir));

        var gitignore = Path.Combine(root, ".gitignore");
        if (!File.Exists(gitignore) || force)
        {
            var existing = File.Exists(gitignore) ? File.ReadAllText(gitignore) : "";
            if (!existing.Contains(".morph/credentials.json", StringComparison.Ordinal))
            {
                File.AppendAllText(gitignore, """

                    # Morph CLI local state
                    .morph/credentials.json
                    """);
            }
        }

        WriteSampleDefinitions(root);
        WriteSamplePolicy(root);

        Console.WriteLine($"Initialized Morph project '{name}' in {root}");
        Console.WriteLine("  morph.yaml");
        Console.WriteLine("  morph/definitions/");
        Console.WriteLine("  morph/policies/");
        Console.WriteLine("Next: morph login  then  morph link --project <id> --env <id>");
        return 0;
    }

    private static void WriteSampleDefinitions(string root)
    {
        var personPath = Path.Combine(root, ProjectPaths.DefinitionsDir, "person.entity.yaml");
        if (!File.Exists(personPath))
        {
            MorphYamlSerializer.SaveDefinition(personPath, new DefinitionIr
            {
                Kind = "Entity",
                Slug = "person",
                DisplayName = "Person",
                Fields =
                [
                    new FieldIr
                    {
                        FieldId = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                        Slug = "fullName",
                        DisplayName = "Full Name",
                        DataType = "String",
                        IsRequired = true,
                        IsIndexed = true
                    },
                    new FieldIr
                    {
                        FieldId = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                        Slug = "email",
                        DisplayName = "Email",
                        DataType = "String",
                        IsRequired = true,
                        IsUnique = true,
                        IsIndexed = true
                    }
                ]
            });
        }

        var employmentPath = Path.Combine(root, ProjectPaths.DefinitionsDir, "employment.component.yaml");
        if (!File.Exists(employmentPath))
        {
            MorphYamlSerializer.SaveDefinition(employmentPath, new DefinitionIr
            {
                Kind = "Component",
                Slug = "employment",
                DisplayName = "Employment Details",
                Fields =
                [
                    new FieldIr
                    {
                        FieldId = Guid.Parse("22222222-2222-2222-2222-222222222201"),
                        Slug = "title",
                        DisplayName = "Title",
                        DataType = "String",
                        IsRequired = true
                    },
                    new FieldIr
                    {
                        FieldId = Guid.Parse("22222222-2222-2222-2222-222222222202"),
                        Slug = "startedOn",
                        DisplayName = "Started On",
                        DataType = "DateTime"
                    }
                ]
            });
        }

        var employeePath = Path.Combine(root, ProjectPaths.DefinitionsDir, "employee.entity.yaml");
        if (!File.Exists(employeePath))
        {
            MorphYamlSerializer.SaveDefinition(employeePath, new DefinitionIr
            {
                Kind = "Entity",
                Slug = "employee",
                DisplayName = "Employee",
                ComponentUses =
                [
                    new ComponentUseIr
                    {
                        ComponentUseId = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                        Alias = "person",
                        TargetSlug = "person",
                        Mode = "Embed",
                        PinLatest = true
                    },
                    new ComponentUseIr
                    {
                        ComponentUseId = Guid.Parse("33333333-3333-3333-3333-333333333302"),
                        Alias = "employment",
                        TargetSlug = "employment",
                        Mode = "Embed",
                        PinLatest = true
                    }
                ],
                Fields =
                [
                    new FieldIr
                    {
                        FieldId = Guid.Parse("33333333-3333-3333-3333-333333333303"),
                        Slug = "employeeNumber",
                        DisplayName = "Employee Number",
                        DataType = "String",
                        IsRequired = true,
                        IsUnique = true,
                        IsIndexed = true
                    }
                ]
            });
        }
    }

    private static void WriteSamplePolicy(string root)
    {
        var path = Path.Combine(root, ProjectPaths.PoliciesDir, "employee.viewer.policy.yaml");
        if (File.Exists(path))
        {
            return;
        }

        MorphYamlSerializer.SavePolicy(path, new PolicyIr
        {
            Name = "employee.viewer",
            Effect = "Allow",
            Actions = ["read", "list"],
            Resources = ["entity:employee"],
            Publish = true
        });
    }
}
