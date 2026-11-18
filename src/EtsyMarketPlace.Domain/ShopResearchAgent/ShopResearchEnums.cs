namespace EtsyMarketPlace.Domain.ShopResearchAgent;

public enum ResearchScheduleCadence
{
    Off = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum ResearchTriggerType
{
    Manual = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum ResearchRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}

public enum CandidateDecisionStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    SentToDraft = 3
}

public enum CandidateRejectionReason
{
    None = 0,
    CopyrightRisk = 1,
    LowMargin = 2,
    OffBrand = 3,
    AlreadySelling = 4,
    Other = 5
}
