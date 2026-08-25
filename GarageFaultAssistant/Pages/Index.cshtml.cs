using GarageFaultAssistant.Models;
using GarageFaultAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GarageFaultAssistant.Pages;

public class IndexModel : PageModel
{
    private readonly IFaultAnalyzer _analyzer;

    public IndexModel(IFaultAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    [BindProperty]
    public string? Description { get; set; }

    public FaultAnalysis? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            ModelState.AddModelError(nameof(Description), "Please enter a description of the fault.");
            return Page();
        }

        try
        {
            Result = await _analyzer.AnalyzeAsync(Description, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to analyse the description: {ex.Message}";
        }

        return Page();
    }
}
