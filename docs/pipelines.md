# NpgsqlAnalyzer pipelines

| Pipeline | Target branches | Tasks | Published package |
| :--- | :--- | :--- | :--- |
| Regular | Include `*`<br/>Exclude `/master /release/*`| build, test | None |
| Beta | `/master`<br/> | build, test, package | Beta version (Azure DevOps feed) |
| Official | `/release/*` | build, test, package | Official version (NuGet feed) |

## Usage examples

### Implementing features, tasks, bug fixes

1. Create branch `/feat/awesome-addition` from `/dev`
2. Implement work
3. Push to origin
4. _Regular_ pipeline gets triggered for branch `/feat/awesome-addition`
5. Create pull request into `/dev`
6. Merge into `/dev`
7. _Regular_ pipeline gets triggered for branch `/dev`

### Beta releases

Beta releases are intended for limited, yet public access to unreleased package versions. These releases should not be used in production software as they are not intended to be retained for extended periods of time and will very often be deleted right after being officially released.

1. Create pull request from `/dev` to `/master`
    - This is preferable, but any branch can be requested to be merged into `/master`, for example hot bug fixes.
2. Merge into `/master`
3. _Beta_ pipeline gets triggered
4. A beta package gets published to the Azure DevOps feed
   - The version of the package will be constructed from the `version.txt` file contained in the repository, which specifies the `(Major).(Minor).(Patch)` version, and the `-beta-(BuildNumber)` postfix. For example, `1.0.2-beta-3`

### Official releases

1. Create a new branch with the following name scheme `/release/(Major).(Minor).(Patch)` 
3. _Official_ pipeline gets triggered
4. An official package is built, tested and published to NuGet
    - The version of the package will be the same as the version specified in the `version.txt` file, contained in the repository