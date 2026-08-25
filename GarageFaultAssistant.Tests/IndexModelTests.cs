using GarageFaultAssistant.Models;
using GarageFaultAssistant.Pages;
using GarageFaultAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GarageFaultAssistant.Tests;

public class IndexModelTests
{
    [Fact]
    public async Task OnPostAsync_WithValidDescription_SetsResult()
    {
        var analyzer = new FakeFaultAnalyzer();
        var page = new IndexModel(analyzer) { Description = "Grinding when braking" };
        var result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.Result);
        Assert.Equal("Customer describes a possible concern related to Braking system.", page.Result.Summary);
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyDescription_ReturnsPageWithModelError()
    {
        var analyzer = new FakeFaultAnalyzer();
        var page = new IndexModel(analyzer) { Description = "" };
        var result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_WhenAnalyzerThrows_SetsErrorMessage()
    {
        var throwingAnalyzer = new ThrowingFaultAnalyzer();
        var page = new IndexModel(throwingAnalyzer) { Description = "Some fault" };
        var result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.ErrorMessage);
        Assert.Contains("boom", page.ErrorMessage);
    }

    private class ThrowingFaultAnalyzer : IFaultAnalyzer
    {
        public Task<FaultAnalysis> AnalyzeAsync(string description, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
