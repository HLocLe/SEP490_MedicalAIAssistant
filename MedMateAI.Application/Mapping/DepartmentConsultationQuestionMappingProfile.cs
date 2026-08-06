using AutoMapper;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Mapping;

public sealed class DepartmentConsultationQuestionMappingProfile : Profile
{
    public DepartmentConsultationQuestionMappingProfile()
    {
        CreateMap<DepartmentConsultationQuestion, DepartmentConsultationQuestionResponse>();
    }
}
