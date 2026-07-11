using AutoMapper;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Mapping;

public sealed class PatientProfileMappingProfile : Profile
{
    public PatientProfileMappingProfile()
    {
        CreateMap<PatientProfile, PatientProfileResponse>()
            .ForMember(
                dest => dest.ChronicDiseases,
                opt => opt.MapFrom(src => src.ChronicDiseases.Where(disease => !disease.IsDeleted)));

        CreateMap<PatientChronicDisease, PatientChronicDiseaseResponse>();
    }
}
