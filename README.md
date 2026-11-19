# PlayerWallet

A .NET 11 console wallet exercise: deposit, withdraw, and bet against an
in-memory wallet that starts at zero for each session.

## Run

```sh
dotnet restore PlayerWallet.slnx
dotnet build PlayerWallet.slnx --configuration Release
dotnet test PlayerWallet.slnx --configuration Release --no-build
dotnet run --project src/PlayerWallet.Console --configuration Release
```

Requires .NET SDK 11.0.100, as pinned by `global.json`. No database or container
runtime is needed.

## Commands

| Command | Meaning |
| --- | --- |
| `deposit <amount>` | Add funds |
| `withdraw <amount>` | Remove funds |
| `bet <amount>` | Play one round |
| `exit` | End the session |

Amounts use a decimal point and cent precision. Invalid input prints a message,
leaves the balance unchanged, and keeps the session running. `exit` and
end-of-input end the session.

## Game rules

- Bets are accepted from $1.00 through $10.00 inclusive.
- Each round draws one category: 50% loss, 40% small win, 10% large win.
- A loss pays $0. A small win pays uniformly in whole cents from $0.01 through
  twice the stake, so the drawn win is anything from near zero to 2x the stake,
  not always 2x. A large win pays uniformly in whole cents above twice the stake
  through ten times the stake.
- The payout is gross winnings: `new balance = old balance - bet + payout`.
- The brief does not specify the distribution inside a band, so uniform sampling
  is used. Small wins then average 1x the stake and large wins average 6x, so
  the expected return is `0.4 * 1x + 0.1 * 6x = 1x`. Paying 2x on every small
  win would return 1.4x instead. A small win can therefore be less than the
  stake.

## Design decisions

| Decision | Why |
| --- | --- |
| One console project, split by folder | There is one executable and one implementation of each contract. Extra projects would add references and build steps without separating anything that changes independently. |
| `Wallet` owns money invariants; `SlotGame` owns the game rules | The wallet can settle any stake its balance covers, so another game can reuse it. The $1–$10 limit and the payout bands have exactly one home. |
| Optimistic concurrency (`Version` plus a conditional save) | The store decides how a stale write is detected; the in-memory dictionary and a future database implement the same contract. A conflict fails the operation instead of retrying, because a retry would reroll the round. |
| Rounds accept an optional `Random` | Production passes `Random.Shared`; tests pass a seeded or scripted source, so a whole round is reproducible and the expected return can be asserted. |
| `decimal` dollars with a cent-precision invariant | Exact at these ranges and readable at the call site. A production ledger would store integer minor units, and only `Money` would change. |
| Rules as constants, not configuration | One regulated game. A per-jurisdiction return is a compliance decision, not a toggle; every constant lives in `SlotGame`. |
| No ledger, database, or DI container | Nothing in the console can observe a history, and no interface has a second implementation yet. |

If this grew into a platform, the first additions would be an append-only ledger
keyed by wallet ID and version, a repository backed by a database transaction,
and idempotency keys on deposits once a payment provider is involved.

## Example

```text
Please, submit action:
deposit 10
Your deposit of $10 was successful. Your current balance is: $10

Please, submit action:
bet 5
Congrats - you won $35.35! Your current balance is: $40.35

Please, submit action:
bet 5
No luck this time! Your current balance is: $35.35

Please, submit action:
withdraw 44.00
Insufficient funds. Your current balance is: $35.35

Please, submit action:
exit
Thank you for playing! Hope to see you again soon.

Press any key to exit.
```

Whole amounts print without cents (`$10`); other amounts keep two decimals
(`$35.35`).

## Structure

One console project, split by folder:

| Folder | Responsibility |
| --- | --- |
| `Domain/` | Money, wallet invariants, fixed game rules |
| `Application/` | Wallet use cases, storage contract, operation result |
| `Infrastructure/` | In-memory repository |
| Project root | Command parsing, console I/O, startup |

The wallet lives in memory and is lost when the process exits.

## Quality

Warnings are errors, analyzers run at the .NET 11 recommended level, and NuGet
audit fails a restore that pulls a known vulnerability. CI verifies formatting,
builds, and runs the tests with coverage.
