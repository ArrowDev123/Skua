# Building Skua

Skua is now built from the Avalonia applications. The legacy WPF projects and WiX installer have been removed.

## Debug builds

Build the complete active solution:

```powershell
dotnet build .\Skua.sln -c Debug -m:1
```

Run either application:

```powershell
dotnet run --project .\Skua.Manager.Avalonia\
dotnet run --project .\Skua.App.Avalonia\
```

The generated Debug executables are:

```text
Skua.Manager.Avalonia\bin\Debug\net10.0\Skua.Manager.exe
Skua.App.Avalonia\bin\Debug\net10.0-windows\Skua.exe
```

## Velopack release

Velopack packages both Avalonia applications into one Windows release. The manager is the package entry point and launches `Skua.exe` from the same package directory.

The script follows Velopack's documented build/stage/package workflow:
[packaging overview](https://docs.velopack.io/packaging/overview),
[CLI reference](https://docs.velopack.io/reference/cli), and
[application integration](https://docs.velopack.io/integrating/overview).

```powershell
.\Publish-Velopack.ps1 -Configuration Release -Runtime win-x64
```

For a local nightly package, use the nightly channel and a commit-qualified version:

```powershell
.\Publish-Velopack.ps1 -Configuration Release -Runtime win-x64 -Channel nightly -Version 2.0.0-5c56629
```

The script builds the solution, stages both Avalonia outputs, then runs the pinned Velopack CLI (`vpk` 1.2.0). Install `vpk` globally with:

```powershell
dotnet tool install --global vpk --version 1.2.0
```

Release files are written to `artifacts\velopack\releases`.

## CI/CD

`.github/workflows/validation.yml` restores and builds Debug and Release for pushes and pull requests, and is reused as the release gate. `.github/workflows/release-stable.yml` packages the `win` channel when a plain `v2.0.0` tag is pushed. `.github/workflows/release-nightly.yml` packages the `nightly` channel on default-branch commits, replacing the single GitHub `nightly` prerelease. `.github/workflows/update-changelog.yml` keeps the client-facing `changelogs.md` synchronized with published stable releases.

Push a tag such as `v2.0.0` to validate, package, and publish a GitHub Release containing the Velopack setup, portable archive, package, and channel metadata.
