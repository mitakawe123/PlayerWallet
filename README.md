# PlayerWallet

A .NET 10 console wallet exercise, organized into Domain, Application,
Infrastructure, and Console projects.

## Current status

The solution and quality tooling are scaffolded. The console prints a setup
message. Wallet operations, persistence adapters, game logic, and behavioral
tests are the next implementation step. The four test projects are intentionally
empty: a successful test command currently verifies the test runner setup, not
application correctness.

## Prerequisites

- .NET SDK 10.0.101 or a later patch in the same feature band, as specified by
  `global.json`. Update this pin deliberately when upgrading the toolchain.
- Access to NuGet.org for package restore and security advisory checks.

No database or container runtime is required for the planned in-memory version.

## Local commands

Run from the repository root:

```sh
dotnet restore PlayerWallet.slnx --locked-mode
dotnet format PlayerWallet.slnx --verify-no-changes --no-restore
dotnet build PlayerWallet.slnx --configuration Release --no-restore
dotnet test PlayerWallet.slnx --configuration Release --no-build --no-restore
dotnet run --project src/PlayerWallet.Console --configuration Release --no-build
```

To apply formatting, run `dotnet format PlayerWallet.slnx --no-restore`.

## Architecture

| Project | Responsibility | Direct project references |
| --- | --- | --- |
| Domain | Wallet invariants, money and game rules | None |
| Application | Use cases, storage contracts, operation results | Domain |
| Infrastructure | In-memory repository and random source adapters | Application, Domain |
| Console | Input/output and startup composition | Application, Infrastructure |

Infrastructure is referenced by Console only to wire implementations at startup.
Command handling should use Application contracts. Domain has no runtime package
dependencies. Analyzer packages are development-only dependencies.

### Planned persistence contract

- An Application-owned `IWalletRepository` will hide a private
  `Dictionary<Guid, WalletSnapshot>` in Infrastructure.
- Repository methods will be asynchronous with cancellation support, allowing a
  future PostgreSQL adapter without changing use cases.
- Loads return independent wallet instances. Mutating a loaded wallet must not
  modify stored state before an explicit save.
- Saves use an expected version, with an atomic comparison and update. A failed
  or conflicting save leaves stored state unchanged.
- A conflicting bet is rejected without silently generating another outcome.
- Each console session creates a new wallet with zero balance.

PostgreSQL will still require schema mapping, migrations, atomic version checks,
and integration tests. Storage contract tests should be reused across adapters.
A transaction ledger is outside the initial scope; if added, ledger entries and
balance updates must be committed atomically.

### Agreed initial behavior

- Commands: `deposit <amount>`, `withdraw <amount>`, `bet <amount>`, `exit`.
- Use `decimal`, accept positive amounts with cent precision, and format balances
  with two decimal places. Use an explicit culture for decimal-point input.
- Reject insufficient funds, fractional cents, invalid commands, and overflow
  without changing the balance. Show the unchanged balance after rejection.
- Bet limits are inclusive: $1.00 through $10.00.
- Each round independently selects a category: 50% loss, 40% small payout,
  10% large payout. These are probabilities, not quotas over a sequence of bets.
- Proposed payout interpretation: loss pays zero; small payouts are uniformly
  selected in whole cents from $0.01 through twice the bet; large payouts are
  uniformly selected in whole cents above twice the bet through ten times the
  bet. This distribution is an explicit assumption because the brief does not
  specify the distribution within each category.
- Payout means gross winnings: `new balance = old balance - bet + payout`.
- Validate the proposed result before committing a balance change.
- Exit and end-of-input both terminate cleanly.

## Quality and dependency policy

- `.editorconfig`: formatting, naming, and diagnostic severity.
- `Directory.Build.props`: shared target framework, nullable analysis, warnings
  as errors, SDK analyzers, deterministic builds, and NuGet auditing.
- `Directory.Packages.props`: central package versions and the private Sonar C#
  analyzer reference shared by every project.
- `tests/Directory.Build.props`: imports root settings and adds test-only tools.
- `nuget.config`: explicit package and audit sources, with source mapping.
- Each project's `packages.lock.json`: committed resolved dependency graph.

Use the SDK's built-in .NET analyzers plus SonarAnalyzer.CSharp and the xUnit
analyzers. Do not add overlapping analyzer suites without a concrete need.
Suppressions should be local, rule-specific, and justified.

NuGet audits direct and transitive dependencies at every severity. Because
warnings are errors, known vulnerability warnings and audit retrieval warnings
fail restore rather than silently skipping security checks. Advisory data can
change independently of the lock files; successful restoration is not a security
guarantee.

To update packages, edit central versions, run `dotnet restore PlayerWallet.slnx
--force-evaluate`, review the lock file changes, and run all checks. Do not disable
audit or remove lock files to get a failing restore to pass.

GitHub Actions runs locked restore, formatting, Release build, and tests.
Dependabot configuration requests weekly NuGet and GitHub Actions update PRs
when the repository is hosted on GitHub and Dependabot is enabled.

## Implementation sequence

1. Domain wallet rules and deterministic unit tests.
2. Application use cases and repository contract.
3. Dictionary repository, random source, and storage contract tests.
4. Console parser, composition, and complete session tests.

Test monetary boundaries, unchanged state on failures, category boundaries with
controlled random values, repository isolation/version conflicts, and graceful
EOF. Avoid statistical tests that expect exact percentages from random samples.
