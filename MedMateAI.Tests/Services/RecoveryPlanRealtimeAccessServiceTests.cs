using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanRealtimeAccessServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRequestRepository> _requestsRepoMock = null!;
    private RecoveryPlanRealtimeAccessService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _requestsRepoMock = new Mock<IRecoveryPlanRequestRepository>();
        _unitOfWorkMock.Setup(u => u.RecoveryPlanRequests).Returns(_requestsRepoMock.Object);
        _service = new RecoveryPlanRealtimeAccessService(_unitOfWorkMock.Object);
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorAccessAsync_AccessFound_ReturnsAccess()
    {
        // Arrange
        var doctorUserId = Guid.NewGuid();
        var data = new RecoveryPlanRealtimeDoctorAccessData(
            DoctorId: Guid.NewGuid(),
            IsActive: true,
            IsAcceptingRecoveryPlanRequests: true,
            IsAccountValid: true);

        _requestsRepoMock.Setup(r => r.GetRealtimeDoctorAccessAsync(doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        // Act
        var result = await _service.GetDoctorAccessAsync(doctorUserId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DoctorId, Is.EqualTo(data.DoctorId));
        Assert.That(result.IsActive, Is.EqualTo(data.IsActive));
        Assert.That(result.IsAcceptingRecoveryPlanRequests, Is.EqualTo(data.IsAcceptingRecoveryPlanRequests));
        Assert.That(result.IsAccountValid, Is.EqualTo(data.IsAccountValid));
    }

    [Test]
    [Category("A")]
    public async Task GetDoctorAccessAsync_AccessNotFound_ReturnsNull()
    {
        // Arrange
        var doctorUserId = Guid.NewGuid();
        _requestsRepoMock.Setup(r => r.GetRealtimeDoctorAccessAsync(doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRealtimeDoctorAccessData?)null);

        // Act
        var result = await _service.GetDoctorAccessAsync(doctorUserId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }
}
