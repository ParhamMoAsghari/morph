# Process engine

See also product guide: [Processes](/guide/processes).

Pipeline:

1. Author YAML / JSON IR  
2. Validate / compile to graph  
3. Publish version  
4. Run instance with input bag  
5. Persist status + activity logs (EF `platform` schema)

Built-ins cover variables, strings, JSON, HTTP, Morph data ops, function invoke, delay, access assert, and control flow. Plugins add activities through `IProcessActivityRegistry`.

Durable **Waiting** status is used for delay (and future wait semantics). Hangfire participates in background work at the Host level.
