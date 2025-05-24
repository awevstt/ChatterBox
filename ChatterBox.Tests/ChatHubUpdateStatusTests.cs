using Xunit;
using Moq;
using ChatterBox.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using ChatterBox.Models;
using ChatterBox.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ChatterBox.Services;

public class ChatHubUpdateStatusTests
{
    [Fact]
    public async Task UpdateStatus_ValidUser_StatusUpdatedAndNotificationSent()
    {
        // Arrange
        var userId = "user123";
        var status = "Away";

        var user = new ApplicationUser { Id = userId, UserName = "TestUser" };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "UpdateStatusTestDb")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Contacts.Add(new Contact { UserId = "contact456", ContactUserId = userId });
        await context.SaveChangesAsync();

        var mockUserManager = MockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
        mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var mockClients = new Mock<IHubCallerClients>();
        var mockClient = new Mock<ISingleClientProxy>();
        mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(mockClient.Object);

        var mockContext = new Mock<HubCallerContext>();
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        mockContext.Setup(c => c.User).Returns(principal);

        ChatHub._userConnectionMap["contact456"] = "connection-id-456";

        var hub = new ChatHub(mockUserManager.Object, context, Mock.Of<INotificationService>())
        {
            Context = mockContext.Object,
            Clients = mockClients.Object
        };

        // Act
        await hub.UpdateStatus(status);

        // Assert
        Assert.Equal(status, user.Status);
        mockClient.Verify(c => c.SendCoreAsync(
            "UserStatusUpdated",
            It.Is<object[]>(o => (string)o[0] == userId && (string)o[1] == status),
            default), Times.Once);
    }

    private Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }
}
