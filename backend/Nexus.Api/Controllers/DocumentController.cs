using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nexus.Data.Services;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Route("api/document")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Gets document metadata and content.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocument(string id)
    {
        var doc = await _documentService.GetDocumentAsync(id);
        if (doc == null)
        {
            return NotFound(new { error = $"Document '{id}' not found." });
        }

        return Ok(new
        {
            id = doc.Id,
            title = doc.Title,
            type = doc.DocumentType,
            employeeName = doc.EmployeeName,
            department = doc.DepartmentName,
            createdAt = doc.CreatedAt,
            contentHtml = doc.ContentHtml
        });
    }

    /// <summary>
    /// Gets document raw HTML content.
    /// </summary>
    [HttpGet("{id}/content")]
    public async Task<IActionResult> GetDocumentContent(string id)
    {
        var doc = await _documentService.GetDocumentAsync(id);
        if (doc == null || string.IsNullOrWhiteSpace(doc.ContentHtml))
        {
            return NotFound(new { error = $"Document '{id}' not found." });
        }

        return Ok(new
        {
            id = doc.Id,
            title = doc.Title,
            contentHtml = doc.ContentHtml
        });
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
