using FinanceManager.Application;
using FinanceManager.Application.Common;
using FinanceManager.Application.Contacts;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Controllers;

public sealed class ContactsControllerTests
{
    private const string ParentAssignmentConflictLocalizationKey = "API_Contacts_Err_Conflict_ParentAssignment";

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (
        ContactsController controller,
        Mock<IContactService> contacts,
        Mock<IParentAssignmentService> parentAssign,
        Mock<IStringLocalizer<FinanceManager.Web.Controllers.Controller>> localizer,
        TestCurrentUserService currentUser)
        Create()
    {
        var contacts = new Mock<IContactService>(MockBehavior.Strict);
        var parentAssign = new Mock<IParentAssignmentService>(MockBehavior.Strict);
        var localizer = new Mock<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();
        var currentUser = new TestCurrentUserService();

        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: false));

        var controller = new ContactsController(
            contacts.Object,
            currentUser,
            parentAssign.Object,
            NullLogger<ContactsController>.Instance,
            localizer.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "trace-contacts-create"
            }
        };

        return (controller, contacts, parentAssign, localizer, currentUser);
    }

    /// <summary>
    /// Verifies conflict contract when assignment fails and rollback delete also fails.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldReturnConflict_WhenParentAssignmentFails_AndRollbackDeleteFails()
    {
        var (controller, contacts, parentAssign, localizer, currentUser) = Create();
        var createdContact = new ContactDto(Guid.NewGuid(), "Inline Contact", ContactType.Other, null, null, false, null);
        var request = new ContactCreateRequest(
            "Inline Contact",
            ContactType.Other,
            null,
            null,
            false,
            new ParentLinkRequest("statement-drafts/entries", Guid.NewGuid(), "ContactId"));

        contacts
            .Setup(s => s.CreateAsync(currentUser.UserId, request.Name, request.Type, request.CategoryId, request.Description, request.IsPaymentIntermediary, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdContact);
        parentAssign
            .Setup(s => s.TryAssignAsync(currentUser.UserId, request.Parent, "contacts", createdContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        contacts
            .Setup(s => s.DeleteAsync(createdContact.Id, currentUser.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        localizer
            .Setup(l => l[ParentAssignmentConflictLocalizationKey])
            .Returns(new LocalizedString(ParentAssignmentConflictLocalizationKey, "Localized parent assignment conflict", resourceNotFound: false));

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var apiError = Assert.IsType<ApiErrorDto>(conflict.Value);
        Assert.Equal("Err_Conflict_ParentAssignment", apiError.code);
        Assert.Equal("Localized parent assignment conflict", apiError.message);
        contacts.VerifyAll();
        parentAssign.VerifyAll();
    }

    /// <summary>
    /// Verifies fallback conflict message is used when localization entry is missing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldUseFallbackConflictMessage_WhenLocalizedResourceMissing()
    {
        var (controller, contacts, parentAssign, localizer, currentUser) = Create();
        var createdContact = new ContactDto(Guid.NewGuid(), "Inline Contact", ContactType.Other, null, null, false, null);
        var request = new ContactCreateRequest(
            "Inline Contact",
            ContactType.Other,
            null,
            null,
            false,
            new ParentLinkRequest("statement-drafts/entries", Guid.NewGuid(), "ContactId"));

        contacts
            .Setup(s => s.CreateAsync(currentUser.UserId, request.Name, request.Type, request.CategoryId, request.Description, request.IsPaymentIntermediary, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdContact);
        parentAssign
            .Setup(s => s.TryAssignAsync(currentUser.UserId, request.Parent, "contacts", createdContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        contacts
            .Setup(s => s.DeleteAsync(createdContact.Id, currentUser.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        localizer
            .Setup(l => l[ParentAssignmentConflictLocalizationKey])
            .Returns(new LocalizedString(ParentAssignmentConflictLocalizationKey, ParentAssignmentConflictLocalizationKey, resourceNotFound: true));

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var apiError = Assert.IsType<ApiErrorDto>(conflict.Value);
        Assert.Equal("Err_Conflict_ParentAssignment", apiError.code);
        Assert.Equal("Contact creation could not be completed because assignment to the requested entry failed.", apiError.message);
        contacts.VerifyAll();
        parentAssign.VerifyAll();
    }
}
