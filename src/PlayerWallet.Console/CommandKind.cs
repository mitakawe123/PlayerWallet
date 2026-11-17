namespace PlayerWallet.Console;

/// <summary>Identifies a supported console action.</summary>
public enum CommandKind
{
    /// <summary>No supported command was recognized.</summary>
    Unknown,

    /// <summary>Add funds to the wallet.</summary>
    Deposit,

    /// <summary>Remove funds from the wallet.</summary>
    Withdraw,

    /// <summary>Play one round.</summary>
    Bet,

    /// <summary>End the session.</summary>
    Exit,
}
