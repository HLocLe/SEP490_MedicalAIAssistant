using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Application.DTOs.ConsultationSessions.Requests;
using MedMateAI.Application.DTOs.ConsultationSessions.Responses;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

public sealed partial class ConsultationSessionService
{
    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> RegisterReminderAsync(
        Guid userId,
        Guid sessionId,
        RegisterConsultationReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return (false, true, Array.Empty<string>());
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" });
        }

        var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
        if (session is null)
        {
            return (false, true, Array.Empty<string>());
        }

        if (session.Status != ConsultationSessionStatus.Completed)
        {
            return (false, false, new[] { "Phiên tư vấn chưa hoàn thành. Vui lòng hoàn tất bước tạo câu hỏi trước." });
        }

        if (!request.EnableReminder)
        {
            session.IsReminderEnabled = false;
            session.UpdatedAt = DateTime.UtcNow;
            _consultationSessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (true, false, Array.Empty<string>());
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var (phoneOk, phoneErrors) = await _userService.UpdateCurrentUserPhoneAsync(
                userId,
                request.PhoneNumber,
                cancellationToken);
            if (!phoneOk)
            {
                return (false, false, phoneErrors);
            }
        }

        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            return (false, false, new[] { "Số điện thoại là bắt buộc để đăng ký nhắc nhở." });
        }

        session.IsReminderEnabled = true;
        session.UpdatedAt = DateTime.UtcNow;
        _consultationSessions.Update(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    public async Task<(bool NotFound, ConsultationSummaryResponse? Data)> GetSummaryAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return (true, null);
        }

        var summary = await BuildSummaryAsync(userId, sessionId, sendReminderSms: false, cancellationToken);
        if (summary is null)
        {
            return (true, null);
        }

        return (false, summary);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, ConsultationSummaryResponse? Data)> CompleteSummaryAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
        if (session is null)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        if (session.Status != ConsultationSessionStatus.Completed)
        {
            return (false, false, new[] { "Phiên tư vấn chưa hoàn thành." }, null);
        }

        var summary = await BuildSummaryAsync(userId, sessionId, sendReminderSms: true, cancellationToken);
        if (summary is null)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        return (true, false, Array.Empty<string>(), summary);
    }

    private async Task<ConsultationSession?> GetOwnedSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await _consultationSessions.FirstOrDefaultAsync(
            x => !x.IsDeleted && x.Id == sessionId && x.UserId == userId,
            cancellationToken: cancellationToken);
    }

    private async Task<ConsultationSummaryResponse?> BuildSummaryAsync(
        Guid userId,
        Guid sessionId,
        bool sendReminderSms,
        CancellationToken cancellationToken)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
        if (session is null || session.Status != ConsultationSessionStatus.Completed)
        {
            return null;
        }

        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var department = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(
            session.DepartmentId,
            cancellationToken);

        string? facilityName = null;
        if (session.FacilityId.HasValue)
        {
            var facility = await _medicalFacilityService.GetMedicalFacilityByIdAsync(
                session.FacilityId.Value,
                cancellationToken);
            facilityName = facility?.FacilityName?.Trim();
        }

        var checklistItems = await GetMergedChecklistItemsAsync(
            session.DepartmentId,
            session.FacilityId,
            cancellationToken);

        var questionsPaged = await _consultationQuestions.GetPagedAsync(
            1,
            100,
            q => !q.IsDeleted && q.ConsultationSessionId == sessionId,
            q => q.OrderBy(x => x.Priority),
            cancellationToken: cancellationToken);

        if (sendReminderSms
            && session.IsReminderEnabled
            && !session.ReminderSmsSentAt.HasValue
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && session.AppointmentTime.HasValue)
        {
            var smsContent = ConsultationReminderSmsBuilder.Build(
                user.DisplayName ?? user.UserName ?? "Ban",
                user.DateOfBirth,
                user.PhoneNumber!,
                department?.DepartmentName ?? string.Empty,
                facilityName ?? "Chua cap nhat",
                session.AppointmentTime);

            // Remind 1 hour before appointment; if already within 1 hour, send immediately.
            var remindAtUtc = session.AppointmentTime.Value.ToUniversalTime().AddHours(-1);
            DateTime? scheduledAt = remindAtUtc > DateTime.UtcNow ? remindAtUtc : null;

            var sent = await _smsSender.SendAsync(
                user.PhoneNumber!,
                smsContent,
                scheduledAt,
                cancellationToken);

            if (sent)
            {
                session.ReminderSmsSentAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;
                _consultationSessions.Update(session);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return new ConsultationSummaryResponse
        {
            SessionId = session.Id,
            User = new ConsultationSummaryUserInfoResponse
            {
                DisplayName = user.DisplayName ?? user.UserName ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
            },
            DepartmentId = session.DepartmentId,
            DepartmentName = department?.DepartmentName?.Trim() ?? string.Empty,
            FacilityId = session.FacilityId,
            FacilityName = facilityName,
            AppointmentTime = session.AppointmentTime,
            Symptoms = session.UserSymptoms?.Trim() ?? string.Empty,
            Status = session.Status,
            IsReminderEnabled = session.IsReminderEnabled,
            ReminderSmsSent = session.ReminderSmsSentAt.HasValue,
            ChecklistItems = checklistItems,
            Questions = questionsPaged.Items
                .Select(question => new ConsultationQuestionResponse
                {
                    Id = question.Id,
                    QuestionText = question.QuestionText,
                    Category = question.Category,
                    Priority = question.Priority,
                })
                .ToList(),
        };
    }

    private async Task<IReadOnlyList<ChecklistItemResponse>> GetMergedChecklistItemsAsync(
        Guid departmentId,
        Guid? facilityId,
        CancellationToken cancellationToken)
    {
        var departmentItems = await _checklistItemService.GetByDepartmentIdAsync(departmentId, cancellationToken);
        var merged = new Dictionary<Guid, ChecklistItemResponse>();

        foreach (var item in departmentItems)
        {
            merged[item.Id] = item;
        }

        if (facilityId.HasValue && facilityId.Value != Guid.Empty)
        {
            var facilityItems = await _checklistItemService.GetByFacilityIdAsync(facilityId.Value, cancellationToken);
            foreach (var item in facilityItems)
            {
                merged[item.Id] = item;
            }
        }

        return merged.Values
            .OrderByDescending(item => item.IsMandatory)
            .ThenBy(item => item.Content)
            .ToList();
    }
}
