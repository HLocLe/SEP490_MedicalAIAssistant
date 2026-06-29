using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

    public sealed class DoctorInvitation : BaseEntity
    {
        public string Email { get; set; } = string.Empty;

        public string TokenHash { get; set; } = string.Empty;

        public Guid? DoctorId { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        public Guid? CreatedByAdminId { get; set; }

        public DoctorInvitationStatus Status { get; set; } = DoctorInvitationStatus.Pending;

        public Doctor? Doctor { get; set; }
    }
