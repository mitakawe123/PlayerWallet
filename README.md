# PlayerWallet

A .NET 11 console wallet exercise: deposit, withdraw, and bet against an
in-memory wallet that starts at zero for each session.

## Run

```sh
dotnet restore PlayerWallet.slnx --locked-mode
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
  twice the stake. A large win pays uniformly in whole cents above twice the
  stake through ten times the stake.
- The payout is gross winnings: `new balance = old balance - bet + payout`.
- The brief does not specify the distribution inside a band, so uniform sampling
  is used. With these bands the expected return is about 100% of the stake, and
  a small win can be less than the stake.

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

Warnings are errors, analyzers run at the .NET 11 recommended level, and CI
restores locked dependencies, verifies formatting, builds, and runs the tests.
