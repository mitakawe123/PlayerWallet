# PlayerWallet

A .NET 11 console wallet exercise, organized into Domain, Application,
Infrastructure, and Console projects.

## Current status

The console application supports deposits, withdrawals, and simulated bets against
an in-memory wallet. Each session starts at zero and exits on `exit` or EOF.
The tests cover domain rules, use cases, repository concurrency, and console flows.

```text
Please, submit action:
deposit 10
Your deposit of $10 was successful. Your current balance is: $10

Please, submit action:
bet 5
Congrats - you won $35.35! Your current balance is: $40.35

Please, submit action:
exit
Thank you for playing! Hope to see you again soon.

Press any key to exit.
```

Amounts use a decimal point and cent precision. Whole amounts are shown without
cents (for example `$10`), and other amounts keep two decimal places. Invalid
input does not terminate the session or change the balance. Payouts are gross
winnings, not net profit.

## Prerequisites

- .NET SDK 11.0.100 or a later patch in the same feature band, as specified by
  `global.json`. Update this pin deliberately when upgrading the toolchain.
- Access to NuGet.org for package restore and security advisory checks.

No database or container runtime is required for the in-memory version.

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

### Persistence contract

- An Application-owned `IWalletRepository` hides a private
  `Dictionary<Guid, Wallet>` in Infrastructure.
- Repository methods are asynchronous with cancellation support, allowing a
  future PostgreSQL adapter without changing use cases.
- Loads return independent wallet instances. Mutating a loaded wallet must not
  modify stored state before an explicit save.
- Saves compare the supplied wallet version with storage, with an atomic comparison and update. A failed
  or conflicting save leaves stored state unchanged.
- A conflicting bet is rejected without silently generating another outcome.
- Each console session creates a new wallet with zero balance.

PostgreSQL will still require schema mapping, migrations, atomic version checks,
and integration tests. Storage contract tests should be reused across adapters.
A transaction ledger is outside the initial scope; if added, ledger entries and
balance updates must be committed atomically.

### Agreed initial behavior

- Commands: `deposit <amount>`, `withdraw <amount>`, `bet <amount>`, `exit`.
- Use `decimal`, accept positive amounts with cent precision, and format whole
  amounts without cents and other amounts with two decimal places. Use an
  explicit culture for decimal-point input.
- Reject insufficient funds, fractional cents, invalid commands, and overflow
  without changing the balance. Show the unchanged balance after rejection.
- Bet limits are inclusive: $1.00 through $10.00.
- Each round independently selects a category: 50% loss, 40% small payout,
  10% large payout. These are probabilities, not quotas over a sequence of bets.
- Payout interpretation: loss pays zero; small payouts are uniformly selected in
  whole cents from $0.01 through twice the bet; large payouts are uniformly
  selected in whole cents above twice the bet through ten times the bet. The
  brief does not specify the distribution within each category, so uniform
  sampling is used. These bands and percentages imply an expected return of about
  100% of the stake, so no house edge is assumed.
- A zero payout is reported as a loss; any positive payout is reported as a win.
  Because a small payout can be below the stake, a small win can still reduce the
  balance; the rules fix the payout band, not a minimum profit.
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

## Implementation and test coverage

1. Domain: wallet rules, exact cent arithmetic, and deterministic game outcomes.
2. Application: wallet use cases, expected errors, and conditional saves.
3. Infrastructure: dictionary snapshots, atomic version checks, and random source.
4. Console: parser, startup composition, error recovery, and complete sessions.

Test monetary boundaries, unchanged state on failures, category boundaries with
controlled random values, repository isolation/version conflicts, and graceful
EOF. Avoid statistical tests that expect exact percentages from random samples.

## Public API documentation

Every public type, property, and method must have meaningful XML `<summary>`
documentation. `GenerateDocumentationFile` enables compiler documentation checks;
CS1591 fails the build for missing XML documentation. StyleCop rules SA1604
and SA1606 fail the build for missing or empty summaries in existing XML
documentation. These checks also apply to tests.
Positional record members are documented through their `<param>` documentation.
The compiler detects missing documentation; review also checks that summaries
explain the member rather than merely repeating its name.

## Numeric and storage limits

`Money.MaximumAmount` is `decimal.MaxValue / 100`, ensuring all supported amounts
retain exact cents, even near the upper limit. Values outside that range are
rejected. Game limits are validated and capped at $1,000,000 so payout sampling
fits integer cent bounds; the default bet range remains $1 through $10.

The repository is process-local and intentionally loses data on exit. Its lock
covers snapshot reads and conditional saves, not the whole use case. Conflicting
saves return an error without rerolling. Unexpected infrastructure errors end the
console session with a nonzero exit code; no unconfirmed payout is shown.

## Source layout and spacing

Keep exactly one declared type per C# file, with the filename matching the type.
This applies to classes, interfaces, records, enums, and test helpers; avoid nested
helper types. Separate properties and methods with a blank line, keeping each XML
summary directly above its member. Follow `.editorconfig` for indentation.

## Input and value conventions

Commands are represented by `CommandKind`. The parser reads `ReadOnlySpan<char>`
slices, compares command names without case sensitivity, and parses amounts with
invariant culture. Console copy and formats live in `ConsoleMessages`; exception
messages remain constants in the class that owns the validation.

Construct money with `new Money(amount)`. The constructor rejects negative,
oversized, and fractional-cent values. Application input validation uses
`Money.IsValid` before construction to return normal validation errors without
throwing. Zero is valid money, but deposit, withdrawal, and bet amounts must be
positive. This console application uses ordinary `await` without
`ConfigureAwait(false)`.

## Additional C# conventions

Use `is` / `is not` for constant comparisons where behavior is equivalent. Keep
`==` / `!=` for comparisons against runtime values, which cannot be constant
patterns. Do not use value tuples: use a named type in its own file instead.
`WalletService` implements each operation explicitly, without delegate-based
dispatch. A shared save method handles persistence and version conflicts.
`OperationResult` carries the outcome returned to callers.

EditorConfig enforces braces, namespace and using placement, explicit
accessibility, variable style, readonly fields, pattern preferences, and naming.
Warnings fail the build. The SDK does not provide a blanket rule banning value
tuples or converting every constant equality to `is`; those remain review rules.

## One wallet model

`Wallet` is the single wallet type. It owns the identity, balance, and persisted
version. The repository stores a copy on create/save and returns a new copy on
load, so unsaved mutations cannot change stored state. Saving compares
`Wallet.Version` and stores a new copy with the next version. Reload before a
subsequent save; the caller's original instance retains its loaded version.
