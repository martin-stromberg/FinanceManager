namespace FinanceManager.Shared.Dtos.Contacts;

/// <summary>
/// Defines the type/category of a contact.
/// </summary>
public enum ContactType
{
    /// <summary>
    /// The contact refers to the current user/account itself.
    /// </summary>
    Self = 0,

    /// <summary>
    /// The contact represents a bank or financial institution.
    /// </summary>
    Bank = 1,

    /// <summary>
    /// The contact is a natural person.
    /// </summary>
    Person = 2,

    /// <summary>
    /// The contact is an organization or company.
    /// </summary>
    Organization = 3,

    /// <summary>
    /// Any other contact type not covered by the predefined values.
    /// </summary>
    Other = 9
}
