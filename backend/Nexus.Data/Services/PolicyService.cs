using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IPolicyService
{
    Task<IEnumerable<PolicyDto>> GetAllAsync(string? category = null, string? search = null);
    Task<PolicyDto?> GetByIdAsync(int id);
    Task<PolicyDto> CreateAsync(CreatePolicyDto dto);
    Task<PolicyDto?> UpdateAsync(int id, UpdatePolicyDto dto);
    Task<bool> DeleteAsync(int id);
}

public class PolicyService : IPolicyService
{
    private readonly NexusDbContext _db;

    public PolicyService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<PolicyDto>> GetAllAsync(string? category = null, string? search = null)
    {
        var query = _db.Policies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category.ToLower() == category.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.Code.ToLower().Contains(s) ||
                                     p.Title.ToLower().Contains(s) ||
                                     p.Category.ToLower().Contains(s) ||
                                     p.ContentSummary.ToLower().Contains(s));
        }

        var policies = await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        return policies.Select(p => new PolicyDto
        {
            Id = p.Id,
            Code = p.Code,
            Title = p.Title,
            Category = p.Category,
            ContentSummary = p.ContentSummary,
            DocumentPath = p.DocumentPath,
            IsActive = p.IsActive,
            UpdatedAt = p.UpdatedAt
        });
    }

    public async Task<PolicyDto?> GetByIdAsync(int id)
    {
        var p = await _db.Policies.AsNoTracking().FirstOrDefaultAsync(pol => pol.Id == id);
        if (p == null) return null;

        return new PolicyDto
        {
            Id = p.Id,
            Code = p.Code,
            Title = p.Title,
            Category = p.Category,
            ContentSummary = p.ContentSummary,
            DocumentPath = p.DocumentPath,
            IsActive = p.IsActive,
            UpdatedAt = p.UpdatedAt
        };
    }

    public async Task<PolicyDto> CreateAsync(CreatePolicyDto dto)
    {
        var policy = new Policy
        {
            Code = string.IsNullOrWhiteSpace(dto.Code) ? $"POL-HR-00{Random.Shared.Next(10, 99)}" : dto.Code.Trim(),
            Title = dto.Title.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "HR" : dto.Category.Trim(),
            ContentSummary = dto.ContentSummary.Trim(),
            DocumentPath = dto.DocumentPath,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();

        return new PolicyDto
        {
            Id = policy.Id,
            Code = policy.Code,
            Title = policy.Title,
            Category = policy.Category,
            ContentSummary = policy.ContentSummary,
            DocumentPath = policy.DocumentPath,
            IsActive = policy.IsActive,
            UpdatedAt = policy.UpdatedAt
        };
    }

    public async Task<PolicyDto?> UpdateAsync(int id, UpdatePolicyDto dto)
    {
        var policy = await _db.Policies.FindAsync(id);
        if (policy == null) return null;

        policy.Code = dto.Code.Trim();
        policy.Title = dto.Title.Trim();
        policy.Category = dto.Category.Trim();
        policy.ContentSummary = dto.ContentSummary.Trim();
        if (dto.DocumentPath != null) policy.DocumentPath = dto.DocumentPath;
        policy.IsActive = dto.IsActive;
        policy.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new PolicyDto
        {
            Id = policy.Id,
            Code = policy.Code,
            Title = policy.Title,
            Category = policy.Category,
            ContentSummary = policy.ContentSummary,
            DocumentPath = policy.DocumentPath,
            IsActive = policy.IsActive,
            UpdatedAt = policy.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var policy = await _db.Policies.FindAsync(id);
        if (policy == null) return false;

        _db.Policies.Remove(policy);
        await _db.SaveChangesAsync();
        return true;
    }
}
