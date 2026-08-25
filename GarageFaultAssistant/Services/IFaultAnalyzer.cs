namespace GarageFaultAssistant.Services;

public interface IFaultAnalyzer
{
    Task<Models.FaultAnalysis> AnalyzeAsync(string description, CancellationToken cancellationToken = default);
}
