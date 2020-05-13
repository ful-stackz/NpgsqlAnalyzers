# NpgsqlAnalyzers

Static code analyzer which provides SQL syntax analysis in the comfort of your C# projects.

## Features

- Static query syntax analysis against a real database.
- Works with multiple variants of query declarations.

## Getting started

### 1. Add a configuration file to your project

To be able to execute the queries against a real database, a connection string to said database is required. Provide the connection string to the `NpgsqlAnalyzers` through the `.npgsqlanalyzers` config file.

- Create a new `.npgsqlanalyzers` file
- Add `CONNECTION_STRING=connection-string-to-your-database` in the file
- Add the following to your `.csproj`:

```
<ItemGroup>
    <AdditionalFiles Include=".npgsqlanalyzers" />
</ItemGroup>
```

Read more about the config file [in the docs](/docs/configuration.md).

### 2. Add the analyzers to your project

Visit [NpgsqlAnalyzers](https://www.nuget.org/packages/NpgsqlAnalyzers) at NuGet for detailed instructions on how to add the analyzers to your project.

## Usage

### Query declarations

Currently, `NpgsqlAnalyzers` can detect queries defined in the following places:

- As a string literal as part of the `NpgsqlCommand` constructor
```csharp
new NpgsqlCommand("SELECT * FROM table", ...);
// Detected query -> SELECT * FROM table
```

- As a local variable passed into the `NpgsqlCommand` constructor
```csharp
string query = "DELETE FROM table";
new NpgsqlCommand(query, ...);
// Detected query -> DELETE FROM table
```

- As a local variable which is re-assigned and passed into the `NpgsqlCommand` constructor
```csharp
string query = "SELECT * FROM table"
new NpgsqlCommand(query, ...);
// Detected query -> SELECT * FROM table

// ...

query = "UPDATE table SET status = 'awesome'";
new NpgsqlCommand(query, ...);
// Detected query -> UPDATE table SET status = 'awesome'
```

- As a string literal passed to the `NpgsqlCommand.CommandText` property
```csharp
var command = new NpgsqlCommand();
command.CommandText = "SELECT * FROM TABLE";
// Detected query -> SELECT * FROM TABLE
```

- As a local variable passed to the `NpgsqlCommand.CommandText` property
```csharp
var query = "DELETE FROM table";
var command = new NpgsqlCommand();
command.CommandText = query;
// Detected query -> DELETE FROM table
```

- As a local variable which is re-assigned and passed to the `NpgsqlCommand.CommandText` property
```csharp
string query = "SELECT * FROM table"
new NpgsqlCommand(query, ...);
// Detected query -> SELECT * FROM table

// ...

query = "UPDATE table SET status = 'awesome'";
var command = new NpgsqlCommand();
command.CommandText = query;
// Detected query -> UPDATE table SET status = 'awesome'
```

### Named parameters

Named parameters like `@id` or `@user_email` are invalid as part of a pure PostgreSQL statement. To avoid unwanted errors while executing the query, named parameters are replaced wil `NULL` inside the query.
A statement containing named parameters, `SELECT * FROM users WHERE username = @username`, becomes `SELECT * FROM users WHERE username = NULL` when executed against the database for analysis.
