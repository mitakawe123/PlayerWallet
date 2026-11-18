using PlayerWallet.Console;
using PlayerWallet.Console.Application;
using PlayerWallet.Console.Infrastructure;

var repository = new InMemoryWalletRepository();
var service = new WalletService(repository);
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
