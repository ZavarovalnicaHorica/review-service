namespace Horica.ReviewService.Domain;

public enum ReviewStatus
{
    /// <summary>Waiting for an employee to pick it up.</summary>
    Pending = 0,

    /// <summary>Assigned to an employee who is working on it.</summary>
    InProgress = 1,

    /// <summary>An employee made a decision.</summary>
    Completed = 2
}

public enum ReviewDecision
{
    Approved = 0,
    Rejected = 1,

    /// <summary>The employee asks the customer for additional documents (UC-7).</summary>
    NeedsMoreInfo = 2
}

public enum ReviewHistoryAction
{
    Created = 0,
    Refreshed = 1,
    Assigned = 2,
    Released = 3,
    Decided = 4
}
