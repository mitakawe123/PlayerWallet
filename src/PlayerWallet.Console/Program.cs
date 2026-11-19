using PlayerWallet.Console;
using PlayerWallet.Console.Application;
using PlayerWallet.Console.Infrastructure;

var repository = new InMemoryWalletRepository();
var service = new WalletService(repository);
var application = new ConsoleApplication(service, Console.In, Console.Out);

int exitCode = await application.RunAsync();
if (!Console.IsInputRedirected)
{
    Console.ReadKey(intercept: true);
}

return exitCode;
