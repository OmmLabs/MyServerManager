# Contributing to Server App Manager

Start with an issue or discussion for a new feature so maintainers can confirm its scope. For a bug, give a small reproducible case using sample services and sanitized diagnostics.

## Dashboard

Use a .NET 10 SDK and run these commands from the repository root.

~~~powershell
dotnet restore ServerAppManager.slnx --locked-mode
dotnet build ServerAppManager.slnx -c Release --no-restore
dotnet test ServerAppManager.slnx -c Release --no-build --no-restore
~~~

The tests use simulated SSH, system commands and HTTP checks. Use a disposable lab for real service operations. Keep changes to commands, privileges and data handling easy to review.

## Website and articles

See the [website guide](website/README.md) to preview the site. Read the [editorial workflow](release-plan/09-website-content-workflow.md) for the briefs, word targets and lab verification. Verify the Docusaurus build before submitting docs or blog changes.

Open a pull request with a short description and what you tested. Follow the [code of conduct](CODE_OF_CONDUCT.md). Send vulnerability details through the private route described in [Security](SECURITY.md).
