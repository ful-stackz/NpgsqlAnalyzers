# Configuring NpgsqlAnalyzers

- [.npgsqlanalyzers config file](#config-file) (v0.0.2+)
- [Environment varialbe](#environment-variable-deprecated) (v0.0.1)

## Config file

Since version `0.0.2` the NpgsqlAnalyzers can be configured through an `.npgsqlanalyzers` config file.

### Adding the config file to the project

To add the config file to your project and make it available to  NpgsqlAnalyzers
add the folowing to your `.csproj`

```
<ItemGroup>
    <AdditionalFile Include=".npgsqlanalyzers" />
</ItemGroup>
```

### Format

The `.npgsqlanalyzers` config file accepts configuration options as single `KEY=VALUE` pairs per line.


- The key/value pairs are separated by `=`
- `#` at the beginning of the line indicates a comment-line; the line will not be processed
- The key and the value are trimmed before usage

```
# This is a comment line, indicated by "#" at the beggining.
# It will not be processed.

# This is a standard key/value pair
KEY=VALUE

# Key and value are trimmed before usage, so
  SPACED_KEY     =      Drunk  value
# will produce the same configuration as
SPACED_KEY=Drunk  value
# KEY = SPACED_KEY
# VALUE = Drunk  value
```


### Configuration options

The configuration file supports the following parameters

| Parameter | Value | Version | Description |
| --------- | ----- | :-----: | ----------- |
| `CONNECTION_STRING` | Valid Npgsql connection string | 0.0.2+ | Connection string poiting to the database against which the queries will be executed in order to be verified. |

## Environment variable (deprecated)

> This type of configuration is deprecated and only used in version `0.0.1`. For more recent versions see [configuration via config file](#config-file).

In version `0.0.1` the connection string to the database that should be used for executing queries and verifying the
result must be set as a machine-wide environment variable.

- `NPGSQLA_CONNECTION_STRING={connection-string}`