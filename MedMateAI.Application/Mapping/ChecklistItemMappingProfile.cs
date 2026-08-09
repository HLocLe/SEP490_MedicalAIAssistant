using AutoMapper;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Mapping;

public sealed class ChecklistItemMappingProfile : Profile
{
    public ChecklistItemMappingProfile()
    {
        CreateMap<ChecklistItem, ChecklistItemResponse>();
    }
}
