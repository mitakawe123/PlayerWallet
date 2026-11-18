using PlayerWallet.Application;
using PlayerWallet.Console;
using PlayerWallet.Domain;
using PlayerWallet.Infrastructure;

var repository = new InMemoryWalletRepository();
var game = new SlotGame(new GameRules(), new SystemRandomSource());
var service = new WalletService(repository, game);
var application = new ConsoleApplication(service, Console.In, Console.Out);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

int exitCode = await application.RunAsync(cancellation.Token);
if (!cancellation.IsCancellationRequested && !Console.IsInputRedirected)
{
    Console.ReadKey(intercept: true);
}

return exitCode;
