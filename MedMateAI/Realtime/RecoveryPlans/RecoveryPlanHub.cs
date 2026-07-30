using System.Security.Claims;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MedMateAI.Realtime.RecoveryPlans;

[Authorize]
public sealed class RecoveryPlanHub : Hub<IRecoveryPlanHubClient>
{
    private const string DoctorIdContextItemKey = "RecoveryPlanDoctorId";
    private const string DoctorQueueTargetCategory = "queue";
    private const string DoctorPrivateTargetCategory = "doctor";
    private const string UserPrivateTargetCategory = "user";

    private readonly IRecoveryPlanRealtimeAccessService _accessService;
    private readonly ILogger<RecoveryPlanHub> _logger;

    public RecoveryPlanHub(
        IRecoveryPlanRealtimeAccessService accessService,
        ILogger<RecoveryPlanHub> logger)
    {
        _accessService = accessService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            Context.Abort();
            return;
        }

        var isDoctor =
            Context.User?.IsInRole(RecoveryPlanRealtimeConstants.DoctorRole) == true;

        if (isDoctor)
        {
            var preliminaryAccess = await _accessService.GetDoctorAccessAsync(
                userId,
                Context.ConnectionAborted);

            if (preliminaryAccess is null || !preliminaryAccess.IsAccountValid)
            {
                Context.Abort();
                return;
            }
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RecoveryPlanRealtimeGroups.User(userId),
            Context.ConnectionAborted);

        if (isDoctor)
        {
            var refreshResult = await RefreshCurrentConnectionDoctorMembershipAsync(
                userId,
                Context.ConnectionAborted);

            if (refreshResult is DoctorMembershipRefreshResult.MissingProfile
                or DoctorMembershipRefreshResult.InvalidAccount)
            {
                await TryRemoveFromGroupAsync(
                    RecoveryPlanRealtimeGroups.User(userId),
                    UserPrivateTargetCategory,
                    Context.ConnectionAborted);

                Context.Abort();
                return;
            }
        }

        await base.OnConnectedAsync();
    }

    [Authorize(Roles = RecoveryPlanRealtimeConstants.DoctorRole)]
    public async Task RefreshDoctorMembershipAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var doctorUserId))
        {
            Context.Abort();
            return;
        }

        var refreshResult = await RefreshCurrentConnectionDoctorMembershipAsync(
            doctorUserId,
            Context.ConnectionAborted);

        if (refreshResult is DoctorMembershipRefreshResult.MissingProfile
            or DoctorMembershipRefreshResult.InvalidAccount)
        {
            Context.Abort();
        }
    }

    [Authorize(Roles = RecoveryPlanRealtimeConstants.DoctorRole)]
    public Task RefreshDoctorQueueMembershipAsync()
    {
        return RefreshDoctorMembershipAsync();
    }

    private async Task<DoctorMembershipRefreshResult>
        RefreshCurrentConnectionDoctorMembershipAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken)
    {
        await TryRemoveFromGroupAsync(
            RecoveryPlanRealtimeGroups.DoctorQueue,
            DoctorQueueTargetCategory,
            cancellationToken);

        if (Context.Items.TryGetValue(
                DoctorIdContextItemKey,
                out var previousDoctorIdValue)
            && previousDoctorIdValue is Guid previousDoctorId)
        {
            await TryRemoveFromGroupAsync(
                RecoveryPlanRealtimeGroups.Doctor(previousDoctorId),
                DoctorPrivateTargetCategory,
                cancellationToken);
        }

        Context.Items.Remove(DoctorIdContextItemKey);

        var access = await _accessService.GetDoctorAccessAsync(
            doctorUserId,
            cancellationToken);

        if (access is null)
        {
            return DoctorMembershipRefreshResult.MissingProfile;
        }

        if (!access.IsAccountValid)
        {
            return DoctorMembershipRefreshResult.InvalidAccount;
        }

        if (!access.IsActive)
        {
            return DoctorMembershipRefreshResult.Inactive;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RecoveryPlanRealtimeGroups.Doctor(access.DoctorId),
            cancellationToken);

        Context.Items[DoctorIdContextItemKey] = access.DoctorId;

        if (access.IsAcceptingRecoveryPlanRequests)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RecoveryPlanRealtimeGroups.DoctorQueue,
                cancellationToken);
        }

        return DoctorMembershipRefreshResult.Active;
    }

    private async Task TryRemoveFromGroupAsync(
        string groupName,
        string targetCategory,
        CancellationToken cancellationToken)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupName,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Recovery Plan Hub membership removal failed for target {TargetCategory}.",
                targetCategory);
        }
    }

    private enum DoctorMembershipRefreshResult
    {
        Active,
        Inactive,
        MissingProfile,
        InvalidAccount
    }
}
