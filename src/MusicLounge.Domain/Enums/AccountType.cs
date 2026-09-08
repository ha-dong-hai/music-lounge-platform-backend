namespace MusicLounge.Domain.Enums;

public enum AccountType
{
    Gateway,
    Platform,

    /// <summary>
    /// Thuế GTGT withheld at source. Named plainly because for a long time it was the only tax this
    /// system knew about; every ledger row ever written to it is VAT, at the 5% services rate, so
    /// the meaning has not changed — only the name is now less specific than the account is.
    /// Renaming it is not an option: AccountType is persisted as a string, and rewriting historical
    /// journal rows would mean editing append-only financial records to fix a label.
    /// </summary>
    Tax,

    /// <summary>
    /// Thuế TNCN withheld at source, kept in its own account rather than added to <see cref="Tax"/>.
    /// They are two different taxes, owed under different rules, at different rates, and remitted
    /// separately — a single combined balance could not answer how much of either is owed.
    /// </summary>
    PersonalIncomeTax,

    User,
    Performer
}
