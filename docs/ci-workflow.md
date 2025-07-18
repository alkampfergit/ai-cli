# Continuous-Integration Workflow (`.github/workflows/ci.yml`)

This document walks through the CI pipeline used by **ai-cli**.

## Trigger Conditions
* **push** – any commit to `main`, `develop`, or branches matching `feature/*`, `hotfix/*`, `bugfix/*`.
* **pull_request** – to `main` or `develop`.

## Job Overview
| Job     | OS / Runner          | Purpose                                                         |
|---------|----------------------|-----------------------------------------------------------------|
| version | ubuntu-latest        | Runs GitVersion once, exposes version info for all other jobs.  |
| test    | Matrix (ubuntu & win)| Restore, build, test, gather coverage. Uses version outputs.    |
| build   | ubuntu-latest        | Publish binaries for 3 platforms. Uses version outputs.         |
| release | ubuntu-latest        | Draft a GitHub release and upload the artifacts. Uses version outputs.|

## Key Steps
1. **version job** – runs first, checks out code and executes GitVersion.  
   * Outputs: `semVer`, `assemblySemVer`, `assemblySemFileVer`, `informationalVersion`, etc.
   * All other jobs depend on this and use its outputs via `needs.version.outputs.*`.
2. **Checkout** – full history (`fetch-depth: 0`) for all jobs.
3. **Cache NuGet** – speeds subsequent runs by caching `~/.nuget/packages`.
4. **Setup .NET** – installs the required SDK (`9.0.x`).
5. **Restore / Build / Test**  
   * Test job runs `dotnet test`, collecting coverage (`XPlat Code Coverage`).  
   * Only the Linux leg uploads coverage to Codecov to avoid duplicate reports.
   * Build and test steps use version info from the `version` job.
6. **Publish** – in the `build` job, `dotnet publish` is executed for:
   * `win-x64`, `linux-x64`, `osx-x64`
   * `--self-contained` single-file executables with trimming enabled.
   * Each output is zipped (`ai-cli-<version>-<rid>.zip`) and stored as an artifact.
   * Uses version info from the `version` job.
7. **Release** – after `build`:
   * Downloads artifacts.
   * Creates a GitHub release (`softprops/action-gh-release`) tagged with the calculated SemVer.
   * Publishes the three binary archives as release assets.
   * Uses version info from the `version` job.

## Best-Practice Notes
* **Single GitVersion execution** – version info is calculated once and reused everywhere, ensuring consistency and faster builds.
* **Matrix `fail-fast: false`** – prevents early cancellation if one OS fails.
* **NuGet caching** – substantially reduces CI duration.
* **Single source of truth** – version numbers flow from the `version` job through build, artifact naming, and release notes.

Feel free to adapt the workflow (e.g., extend platform list, add static analysis) as project needs grow.
