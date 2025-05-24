using System.Security.Claims;
using ChatterBox.Data;
using ChatterBox.Hubs;
using ChatterBox.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class ChatHubConnectionTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<ISingleClientProxy> _singleClientProxyMock;

    public ChatHubConnectionTests()
    {
        // In-memory DB
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("ChatConnectionTestDb")
            .Options;
        _context = new ApplicationDbContext(options);

        // Mock UserManager
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        // Mocks for SignalR
        _clientsMock = new Mock<IHubCallerClients>();
        _singleClientProxyMock = new Mock<ISingleClientProxy>();
        _clientsMock.Setup(x => x.Client(It.IsAny<string>())).Returns(_singleClientProxyMock.Object);
    }

    [Fact]
    public async Task OnConnectedAsync_SetsUserStatusToOnline()
    {
        // Arrange
        var userId = "test-user-id";
        var testUser = new ApplicationUser { Id = userId, UserName = "testuser", Status = "Offline" };
        await _context.Users.AddAsync(testUser);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(testUser);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var claimsIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        });

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(x => x.User).Returns(new ClaimsPrincipal(claimsIdentity));

        var hub = new ChatHub(_userManagerMock.Object, _context, null)
        {
            Context = mockContext.Object,
            Clients = _clientsMock.Object
        };

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.Equal("Online", testUser.Status);
    }
}
