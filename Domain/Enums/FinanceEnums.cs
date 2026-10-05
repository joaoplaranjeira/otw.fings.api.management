namespace otw.fings.api.management.Domain.Enums;

public enum HouseholdRole
{
    Owner = 1,
    Administrator = 2,
    Member = 3,
    Viewer = 4
}

public enum HouseholdRelationship
{
    Self = 1,
    Husband = 2,
    Wife = 3,
    Partner = 4,
    Son = 5,
    Daughter = 6,
    Child = 7,
    Father = 8,
    Mother = 9,
    Parent = 10,
    Brother = 11,
    Sister = 12,
    Sibling = 13,
    Grandfather = 14,
    Grandmother = 15,
    Grandparent = 16,
    OtherRelative = 17,
    Other = 18
}

public enum HouseholdInvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Expired = 3,
    Revoked = 4
}

public enum BudgetStatus
{
    Active = 1,
    Closed = 2,
    Cancelled = 3
}

public enum IncomeOrigin
{
    Manual = 1,
    Budget = 2
}

public enum FinancialRecordStatus
{
    Planned = 1,
    Confirmed = 2,
    Cancelled = 3
}

public enum ExpenseOrigin
{
    Manual = 1,
    Recurring = 2,
    Receipt = 3
}

public enum RecurrenceFrequency
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Annual = 4
}

public enum ReceiptParseValidationStatus
{
    Pending = 1,
    Valid = 2,
    Invalid = 3
}
