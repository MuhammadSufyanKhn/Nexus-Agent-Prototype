using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Data.DTOs;
using Nexus.Data.Services;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyService _policyService;

    public PoliciesController(IPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetAll([FromQuery] string? category, [FromQuery] string? search)
    {
        var policies = await _policyService.GetAllAsync(category, search);
        return Ok(policies);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PolicyDto>> GetById(int id)
    {
        var policy = await _policyService.GetByIdAsync(id);
        if (policy == null) return NotFound(new { message = $"Policy #{id} not found" });
        return Ok(policy);
    }

    [HttpPost]
    public async Task<ActionResult<PolicyDto>> Create([FromBody] CreatePolicyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _policyService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PolicyDto>> Update(int id, [FromBody] UpdatePolicyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _policyService.UpdateAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Policy #{id} not found" });
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _policyService.DeleteAsync(id);
        if (!success) return NotFound(new { message = $"Policy #{id} not found" });
        return NoContent();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<object>> UploadPolicyFile([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No document file was uploaded." });

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "policies");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = $"/policies/{fileName}";
        return Ok(new { documentPath = relativePath, fileName = file.FileName, fileSize = file.Length });
    }
}
