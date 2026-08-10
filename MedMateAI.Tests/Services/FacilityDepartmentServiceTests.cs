using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class FacilityDepartmentServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IFacilityDepartmentRepository> _repoMock = null!;
    private FacilityDepartmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _repoMock = new Mock<IFacilityDepartmentRepository>();
        _unitOfWorkMock.Setup(u => u.FacilityDepartments).Returns(_repoMock.Object);
        _service = new FacilityDepartmentService(_unitOfWorkMock.Object);
    }

    [Test]
    [Category("B")]
    public async Task ListActiveFacilityDepartmentsAsync_SearchNull_ReturnsAllActive()
    {
        // Arrange
        var departments = new List<FacilityDepartment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FacilityId = Guid.NewGuid(),
                DepartmentId = Guid.NewGuid(),
                Facility = new MedicalFacility { FacilityName = "Hospital A" },
                Department = new MedicalDepartment { DepartmentName = "Cardiology" }
            }
        };

        _repoMock.Setup(r => r.GetActiveFacilityDepartmentsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(departments);

        // Act
        var result = await _service.ListActiveFacilityDepartmentsAsync(null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].FacilityName, Is.EqualTo("Hospital A"));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Cardiology"));
    }

    [Test]
    [Category("N")]
    public async Task ListActiveFacilityDepartmentsAsync_WithSearch_ReturnsMatchingActive()
    {
        // Arrange
        var search = "Cardio";
        var departments = new List<FacilityDepartment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FacilityId = Guid.NewGuid(),
                DepartmentId = Guid.NewGuid(),
                Facility = new MedicalFacility { FacilityName = "Hospital A" },
                Department = new MedicalDepartment { DepartmentName = "Cardiology" }
            }
        };

        _repoMock.Setup(r => r.GetActiveFacilityDepartmentsAsync(search, It.IsAny<CancellationToken>()))
            .ReturnsAsync(departments);

        // Act
        var result = await _service.ListActiveFacilityDepartmentsAsync(search, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Cardiology"));
    }
}
