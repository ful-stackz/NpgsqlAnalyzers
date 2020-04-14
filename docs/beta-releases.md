# Beta releases

Beta releases provide an easy way to test the process of installing, using and
removing the `NpgsqlAnalyzers` package from a project,
without publishing unnecessary versions to NuGet. Beta releases are available
through the Azure DevOps private package feed.

## Disclaimer

Beta releases are not intended for use in production code as their lifetime is
short and they are not published with long-time support in mind.

## Get the package feed

To be able to use beta releases, first you need to add the private beta package
feed to your project or environment.

### Project scope

With this method, the beta feed will be available to a single project or solution.
This method is preferable if you don't want to pollute the global package sources.

1. Create `nuget.config` in the same directory as your `.csproj` or `.sln` file
2. Paste the following into the newly created `nuget.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="NpgsqlAnalyzers Beta" value="https://pkgs.dev.azure.com/ivanstoyanov0768/NpgsqlAnalyzers/_packaging/BetaReleases/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

#### Removing source

To remove the package source simply delete the `nuget.config` file or the
following line from the file:

```
<add key="NpgsqlAnalyzers Beta" value="https://pkgs.dev.azure.com/ivanstoyanov0768/NpgsqlAnalyzers/_packaging/BetaReleases/nuget/v3/index.json" />
```

### Global scope

This method makes the beta feed available in all projects in your environment.

To add the feed to your environment, simply execute the following command in your
terminal:

```
dotnet nuget add source https://pkgs.dev.azure.com/ivanstoyanov0768/NpgsqlAnalyzers/_packaging/BetaReleases/nuget/v3/index.json --name "NpgsqlAnalyzers Beta"
```

#### Removing source

To remove the globally available package source execute the following command in your terminal:

```
dotnet nuget remove https://pkgs.dev.azure.com/ivanstoyanov0768/NpgsqlAnalyzers/_packaging/BetaReleases/nuget/v3/index.json
```

## Install the package

After making the the package feed available to your project or environment,
installing the package is the same as usual. You can use your prefered method.

Check out the available versions at the [feed source](https://dev.azure.com/ivanstoyanov0768/NpgsqlAnalyzers/_packaging?_a=package&feed=BetaReleases&package=NpgsqlAnalyzers&protocolType=NuGet&version=0.0.1-beta-44&view=versions).
**Keep in mind** that beta versions are postfixed with `-beta-{build-number}`.

### dotnet cli

Execute the following command in your terminal:

```
dotnet add package NpgsqlAnalyzers --version 1.0.0-beta-1
```

### PackageReference

Add the following line to your `.csproj` file:

```
<PackageReference Include="NpgsqlAnalyzers" Version="1.0.0-beta-1" />
```

### Visual Studio UI

1. Open Visual Studio
2. Go to `Projects` -> `Manage NuGet Packages...`
3. Make sure that you have selected the NpgsqlAnalyzers beta feed
    - Package source selector is located at the top right corner in the
package manager
4. If you don't see the package in the packages list, select the
`Include prerelease` option
    - The option is located next to the package search bar