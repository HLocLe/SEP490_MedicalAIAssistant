using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class MedicalFacility : BaseEntity
{
    public string? FacilityName { get; set; }

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? ImageUrl { get; set; }

    public string? OpeningHours { get; set; }

    public MedicalFacilityType FacilityType { get; set; } = MedicalFacilityType.Hospital;

    public bool IsActive { get; set; }

    public ICollection<FacilityDepartment> FacilityDepartments { get; set; } = new List<FacilityDepartment>();

    public ICollection<FeedbackReview> FeedbackReviews { get; set; } = new List<FeedbackReview>();

    public ICollection<ChecklistItem> ChecklistItems { get; set; } = new List<ChecklistItem>();

    public ICollection<ConsultationSession> ConsultationSessions { get; set; } = new List<ConsultationSession>();

    public ICollection<TreatmentJourney> TreatmentJourneys { get; set; } = new List<TreatmentJourney>();
}
