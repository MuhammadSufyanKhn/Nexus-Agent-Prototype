using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nexus.Data.Services;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Downloads the generated HR document (HTML/PDF format).
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadDocument(string id)
    {
        var doc = await _documentService.GetDocumentAsync(id);
        if (doc == null || string.IsNullOrWhiteSpace(doc.ContentHtml))
        {
            return NotFound(new { error = $"Document '{id}' not found." });
        }

        var bytes = Encoding.UTF8.GetBytes(doc.ContentHtml);
        return File(bytes, "text/html", $"{id}.html");
    }

    /// <summary>
    /// Previews the generated HR document in browser.
    /// </summary>
    [HttpGet("{id}/preview")]
    public async Task<IActionResult> PreviewDocument(string id)
    {
        var doc = await _documentService.GetDocumentAsync(id);
        if (doc == null || string.IsNullOrWhiteSpace(doc.ContentHtml))
        {
            return NotFound(new { error = $"Document '{id}' not found." });
        }

        return Content(doc.ContentHtml, "text/html", Encoding.UTF8);
    }
}
