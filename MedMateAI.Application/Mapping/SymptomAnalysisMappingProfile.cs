using AutoMapper;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.Session;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Mapping;

public sealed class SymptomAnalysisMappingProfile : Profile
{
    public SymptomAnalysisMappingProfile()
    {
        CreateMap<SymptomAnalysisSession, SymptomAnalysisResponse>()
            .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Symptoms, opt => opt.Ignore())
            .ForMember(dest => dest.Answers, opt => opt.Ignore())
            .ForMember(dest => dest.RecommendedDepartments, opt => opt.Ignore());

        CreateMap<SessionSymptom, SessionSymptomResponse>();

        CreateMap<SessionClinicalQuestionAnswer, ClinicalQuestionAnswerResult>()
            .ForMember(dest => dest.QuestionId, opt => opt.MapFrom(src => src.ClinicalQuestionId))
            .ForMember(
                dest => dest.QuestionVi,
                opt => opt.MapFrom(src => src.ClinicalQuestion != null ? src.ClinicalQuestion.QuestionVi : null))
            .ForMember(
                dest => dest.EnglishPrefix,
                opt => opt.MapFrom(src => src.ClinicalQuestion != null ? src.ClinicalQuestion.EnglishPrefix : null))
            .ForMember(dest => dest.Answers, opt => opt.MapFrom(src => src.AnswerValues));

        CreateMap<DepartmentRecommendation, RecommendedDepartmentResponse>()
            .ForMember(
                dest => dest.DepartmentName,
                opt => opt.MapFrom(src => src.Department != null ? src.Department.DepartmentName : null))
            .ForMember(
                dest => dest.IcdChapterCode,
                opt => opt.MapFrom(src => src.Department != null ? src.Department.ChapterCode : null));
    }
}
