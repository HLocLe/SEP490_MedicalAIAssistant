using AutoMapper;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Mapping;

public sealed class LabIndicatorMappingProfile : Profile
{
    public LabIndicatorMappingProfile()
    {
        CreateMap<LabIndicatorMaster, LabIndicatorResponse>()
            .ForMember(dest => dest.IndicatorId, opt => opt.MapFrom(src => src.Id));

        CreateMap<LabIndicatorMaster, LabIndicatorDetailResponse>()
            .ForMember(dest => dest.IndicatorId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Aliases, opt => opt.MapFrom(src => src.LabIndicatorAliases))
            .ForMember(dest => dest.ReferenceRanges, opt => opt.MapFrom(src => src.LabIndicatorReferenceRanges))
            .ForMember(dest => dest.AdviceCaches, opt => opt.MapFrom(src => src.LabIndicatorAdviceCaches));

        CreateMap<LabIndicatorAlias, LabIndicatorAliasResponse>()
            .ForMember(dest => dest.AliasId, opt => opt.MapFrom(src => src.Id));

        CreateMap<LabIndicatorReferenceRange, LabIndicatorReferenceRangeResponse>()
            .ForMember(dest => dest.ReferenceRangeId, opt => opt.MapFrom(src => src.Id));

        CreateMap<LabIndicatorAdviceCache, LabIndicatorAdviceCacheResponse>()
            .ForMember(dest => dest.CacheId, opt => opt.MapFrom(src => src.Id));
    }
}
