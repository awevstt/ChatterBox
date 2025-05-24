using Xunit;
using Moq;
using ChatterBox.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using ChatterBox.Models;
using ChatterBox.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ChatterBox.Services;

public class JoinGroupTest
{
    [Fact]
    public async Task JoinGroup_UserIsMember_AddsToGroupAndSendsNotification()
    {
        // In-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "JoinGroupTestDb")
            .Options;

        using var context = new ApplicationDbContext(options);
        var userId = "test-user-id";
        var groupId = 1;

        // Seed test data
        context.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            WasAdmin = false
        });

        await context.SaveChangesAsync();

        var mockUserManager = MockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(new ApplicationUser { Id = userId, UserName = "TestUser" });

        var mockGroups = new Mock<IGroupManager>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockClientGroup = new Mock<IClientProxy>();

        mockClients.Setup(c => c.Group(groupId.ToString())).Returns(mockClientGroup.Object);

        var mockContextAccessor = new Mock<HubCallerContext>();
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        mockContextAccessor.Setup(c => c.User).Returns(principal);
        mockContextAccessor.Setup(c => c.ConnectionId).Returns("conn-id");

        var hub = new ChatHub(mockUserManager.Object, context, Mock.Of<INotificationService>())
        {
            Context = mockContextAccessor.Object,
            Clients = mockClients.Object,
            Groups = mockGroups.Object
        };

        // Act
        await hub.JoinGroup(groupId);

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("conn-id", groupId.ToString(), default), Times.Once);
        mockClientGroup.Verify(c => c.SendCoreAsync("UserJoinedGroup",
            It.Is<object[]>(o => (int)o[0] == groupId && (string)o[1] == "TestUser"), default), Times.Once);
    }

    private Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }
}
