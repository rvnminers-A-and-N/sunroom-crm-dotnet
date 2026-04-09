using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactRepository _contacts;
    private readonly ITagRepository _tags;

    public ContactsController(IContactRepository contacts, ITagRepository tags)
    {
        _contacts = contacts;
        _tags = tags;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ContactFilterParams filter)
    {
        var userId = GetUserId();
        var (items, total) = await _contacts.GetAllAsync(userId, filter);

        return Ok(new PaginatedResponse<ContactDto>
        {
            Data = items.Select(MapContactDto).ToList(),
            Meta = new PaginationMeta
            {
                CurrentPage = filter.Page,
                PerPage = filter.PerPage,
                Total = total,
                LastPage = (int)Math.Ceiling((double)total / filter.PerPage)
            }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var contact = await _contacts.GetByIdWithDetailsAsync(id);
        if (contact == null) return NotFound();

        return Ok(new ContactDetailDto
        {
            Id = contact.Id,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            Phone = contact.Phone,
            Title = contact.Title,
            Notes = contact.Notes,
            LastContactedAt = contact.LastContactedAt,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
            Company = contact.Company != null ? new CompanyDto
            {
                Id = contact.Company.Id,
                Name = contact.Company.Name,
                Industry = contact.Company.Industry,
                City = contact.Company.City,
                State = contact.Company.State
            } : null,
            Tags = contact.Tags.Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt
            }).ToList(),
            Deals = contact.Deals.Select(d => new DealDto
            {
                Id = d.Id,
                Title = d.Title,
                Value = d.Value,
                Stage = d.Stage.ToString(),
                ContactName = $"{contact.FirstName} {contact.LastName}",
                ContactId = contact.Id,
                CompanyName = d.Company?.Name,
                CompanyId = d.CompanyId,
                ExpectedCloseDate = d.ExpectedCloseDate,
                ClosedAt = d.ClosedAt,
                CreatedAt = d.CreatedAt
            }).ToList(),
            Activities = contact.Activities
                .OrderByDescending(a => a.OccurredAt)
                .Select(a => new ActivityDto
                {
                    Id = a.Id,
                    Type = a.Type.ToString(),
                    Subject = a.Subject,
                    Body = a.Body,
                    AiSummary = a.AiSummary,
                    ContactId = a.ContactId,
                    ContactName = $"{contact.FirstName} {contact.LastName}",
                    DealId = a.DealId,
                    UserName = a.User?.Name ?? "",
                    OccurredAt = a.OccurredAt,
                    CreatedAt = a.CreatedAt
                }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest request)
    {
        var contact = new Contact
        {
            UserId = GetUserId(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Title = request.Title,
            Notes = request.Notes,
            CompanyId = request.CompanyId
        };

        await _contacts.CreateAsync(contact);

        if (request.TagIds?.Any() == true)
            await _contacts.SyncTagsAsync(contact.Id, request.TagIds);

        var created = await _contacts.GetByIdAsync(contact.Id);
        return CreatedAtAction(nameof(GetById), new { id = contact.Id }, MapContactDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateContactRequest request)
    {
        var contact = await _contacts.GetByIdAsync(id);
        if (contact == null) return NotFound();

        contact.FirstName = request.FirstName;
        contact.LastName = request.LastName;
        contact.Email = request.Email;
        contact.Phone = request.Phone;
        contact.Title = request.Title;
        contact.Notes = request.Notes;
        contact.CompanyId = request.CompanyId;

        await _contacts.UpdateAsync(contact);
        return Ok(MapContactDto(contact));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _contacts.ExistsAsync(id))
            return NotFound();

        await _contacts.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> SyncTags(int id, [FromBody] SyncTagsRequest request)
    {
        if (!await _contacts.ExistsAsync(id))
            return NotFound();

        await _contacts.SyncTagsAsync(id, request.TagIds);
        var contact = await _contacts.GetByIdAsync(id);
        return Ok(MapContactDto(contact!));
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ContactDto MapContactDto(Contact c) => new()
    {
        Id = c.Id,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Email = c.Email,
        Phone = c.Phone,
        Title = c.Title,
        CompanyName = c.Company?.Name,
        CompanyId = c.CompanyId,
        LastContactedAt = c.LastContactedAt,
        Tags = c.Tags.Select(t => new TagDto
        {
            Id = t.Id,
            Name = t.Name,
            Color = t.Color,
            CreatedAt = t.CreatedAt
        }).ToList(),
        CreatedAt = c.CreatedAt
    };
}
