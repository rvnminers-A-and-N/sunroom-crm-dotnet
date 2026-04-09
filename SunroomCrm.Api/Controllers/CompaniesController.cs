using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyRepository _companies;

    public CompaniesController(ICompanyRepository companies)
    {
        _companies = companies;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] PaginationParams pagination)
    {
        var userId = GetUserId();
        var (items, total) = await _companies.GetAllAsync(userId, search, pagination);

        return Ok(new PaginatedResponse<CompanyDto>
        {
            Data = items.Select(c => new CompanyDto
            {
                Id = c.Id,
                Name = c.Name,
                Industry = c.Industry,
                Website = c.Website,
                Phone = c.Phone,
                City = c.City,
                State = c.State,
                ContactCount = c.Contacts.Count,
                DealCount = c.Deals.Count,
                CreatedAt = c.CreatedAt
            }).ToList(),
            Meta = new PaginationMeta
            {
                CurrentPage = pagination.Page,
                PerPage = pagination.PerPage,
                Total = total,
                LastPage = (int)Math.Ceiling((double)total / pagination.PerPage)
            }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var company = await _companies.GetByIdWithDetailsAsync(id);
        if (company == null) return NotFound();

        return Ok(new CompanyDetailDto
        {
            Id = company.Id,
            Name = company.Name,
            Industry = company.Industry,
            Website = company.Website,
            Phone = company.Phone,
            Address = company.Address,
            City = company.City,
            State = company.State,
            Zip = company.Zip,
            Notes = company.Notes,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt,
            Contacts = company.Contacts.Select(c => new ContactDto
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                Title = c.Title,
                CompanyName = company.Name,
                CompanyId = company.Id,
                LastContactedAt = c.LastContactedAt,
                CreatedAt = c.CreatedAt
            }).ToList(),
            Deals = company.Deals.Select(d => new DealDto
            {
                Id = d.Id,
                Title = d.Title,
                Value = d.Value,
                Stage = d.Stage.ToString(),
                ContactName = d.Contact != null ? $"{d.Contact.FirstName} {d.Contact.LastName}" : "",
                ContactId = d.ContactId,
                CompanyName = company.Name,
                CompanyId = company.Id,
                ExpectedCloseDate = d.ExpectedCloseDate,
                ClosedAt = d.ClosedAt,
                CreatedAt = d.CreatedAt
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request)
    {
        var company = new Company
        {
            UserId = GetUserId(),
            Name = request.Name,
            Industry = request.Industry,
            Website = request.Website,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Zip = request.Zip,
            Notes = request.Notes
        };

        await _companies.CreateAsync(company);

        return CreatedAtAction(nameof(GetById), new { id = company.Id }, new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            Industry = company.Industry,
            Website = company.Website,
            Phone = company.Phone,
            City = company.City,
            State = company.State,
            ContactCount = 0,
            DealCount = 0,
            CreatedAt = company.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCompanyRequest request)
    {
        var company = await _companies.GetByIdAsync(id);
        if (company == null) return NotFound();

        company.Name = request.Name;
        company.Industry = request.Industry;
        company.Website = request.Website;
        company.Phone = request.Phone;
        company.Address = request.Address;
        company.City = request.City;
        company.State = request.State;
        company.Zip = request.Zip;
        company.Notes = request.Notes;

        await _companies.UpdateAsync(company);
        return Ok(new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            Industry = company.Industry,
            Website = company.Website,
            Phone = company.Phone,
            City = company.City,
            State = company.State,
            CreatedAt = company.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _companies.ExistsAsync(id))
            return NotFound();

        await _companies.DeleteAsync(id);
        return NoContent();
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
