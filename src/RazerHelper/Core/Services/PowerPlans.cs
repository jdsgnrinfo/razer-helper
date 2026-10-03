namespace RazerHelper.Core.Services;

/// <summary>One of Windows' power plans.</summary>
internal sealed record PowerPlan(Guid Id, string Name);

/// <summary>Windows' power plans: which there are, which is active, and making one active.</summary>
internal interface IPowerPlans
{
    IReadOnlyList<PowerPlan> List();

    Guid Active();

    void Activate(Guid plan);
}
