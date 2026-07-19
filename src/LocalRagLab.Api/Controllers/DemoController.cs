using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/demo")]
public sealed class DemoController : ControllerBase
{
    private readonly DocumentIngestionService _ingestionService;

    public DemoController(DocumentIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    /// <summary>
    /// Seeds three small documents: an employee policy, an HR-only policy, and a document
    /// belonging to another tenant. This makes authorization and tenant-isolation easy to test.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        var requests = new[]
        {
            new IngestTextRequest
            {
                TenantId = "company-1",
                DocumentId = "vacation-policy-v7",
                Title = "Employee Vacation Policy",
                RequiredRole = "employee",
                Text = """
                    Vacation Policy - Version 7

                    Employees receive 20 vacation days per calendar year.
                    Employees may carry unused vacation days into the following year.
                    A maximum of 5 unused vacation days may be carried over.
                    Any days above the maximum expire on December 31 unless a written exception is approved by HR.

                    Vacation requests longer than five consecutive workdays require manager approval.
                    """
            },
            new IngestTextRequest
            {
                TenantId = "company-1",
                DocumentId = "salary-policy-private",
                Title = "Confidential Salary Policy",
                RequiredRole = "hr",
                Text = """
                    Salary review information is confidential and may only be accessed by members of the HR team.
                    The annual compensation review takes place during March.
                    """
            },
            new IngestTextRequest
            {
                TenantId = "company-2",
                DocumentId = "vacation-policy-other-tenant",
                Title = "Other Tenant Vacation Policy",
                RequiredRole = "employee",
                Text = """
                    Employees of company-2 may carry over a maximum of 10 unused vacation days.
                    This policy applies only to company-2.
                    """
            }
        };

        var results = new List<IngestDocumentResponse>();

        foreach (var request in requests)
        {
            results.Add(await _ingestionService.IngestTextAsync(
                request,
                cancellationToken));
        }

        return Ok(results);
    }
}
