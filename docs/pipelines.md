# NpgsqlAnalyzer pipelines

| Pipeline | Triggers | Tasks | Published package |
| :--- | :--- | :--- | :--- |
| Regular | All branches | build, test | None |
| Beta | Manual | build, test, package | Beta version (Azure DevOps feed) |
| Official | Manual | build, test, package | Official version (NuGet feed) |

## Regular pipeline

The **_regular_** pipeline provides a standard CI workflow, ensuring all builds succeed and tests pass.
This pipeline is triggered for new branches, branch updates and pull requests.

## Beta releases pipeline

The **_beta_** releases pipeline is _not_ triggered automatically and requires special permissions to trigger it manually.

Beta releases are intended for limited, yet public access to unreleased package versions through a private Azure DevOps feed.
These releases should not be used in production code as they are not intended to be retained for extended periods of time and
will very often be deleted right after being promoted to an official release.

Beta releases follow the standard [SemVer](https://semver.org/) versioning scheme, but are post-fixed to indicate
their beta status. A beta release version has the following scheme `(Major).(Minor).(Patch)-beta-(BuildNumber)`.

## Official releases

The **_official_** releases pipeline is _not_ triggered automatically and requires special permissions to trigger it manually.

Successful runs of this pipeline result in a package published to the standard NuGet feed.
Officially released packages follow the standard [SemVer](https://semver.org/) versioning scheme - `(Major).(Minor).(Patch)`.
