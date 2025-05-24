using System.Security.Claims;
using System.Threading.Tasks;
using ChatterBox.Data;
using ChatterBox.Hubs;
using ChatterBox.Models;
using ChatterBox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class SendMessageToAI
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<ISingleClientProxy> _clientProxyMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ApplicationDbContext _context;

    public SendMessageToAI()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;
        _context = new ApplicationDbContext(options);

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        _clientsMock = new Mock<IHubCallerClients>();
        _clientProxyMock = new Mock<ISingleClientProxy>();
        _clientsMock.Setup(clients => clients.Caller).Returns(_clientProxyMock.Object);

        _notificationServiceMock = new Mock<INotificationService>();
    }

    [Fact]
    public async Task SendMessageToAI_SavesUserMessageAndSendsAIResponse()
    {
        var userId = "test-user-id";
        var user = new ApplicationUser { Id = userId, UserName = "testuser" };
        _userManagerMock.Setup(um => um.FindByIdAsync(userId)).ReturnsAsync(user);

        var hubContext = new Mock<HubCallerContext>();
        hubContext.Setup(c => c.User.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

        var chatHub = new ChatHub(_userManagerMock.Object, _context, _notificationServiceMock.Object)
        {
            Clients = _clientsMock.Object,
            Context = hubContext.Object
        };

        var userMessage = "Як ся маєш";

        await chatHub.SendMessageToAI(userMessage);

        var messages = await _context.AIMessages.ToListAsync();
        Assert.Contains(messages, m => m.UserId == userId && m.Sender == "User" && m.Content == userMessage);
        Assert.Contains(messages, m => m.UserId == userId && m.Sender == "AI");

        _clientProxyMock.Verify(
            client => client.SendCoreAsync("ReceiveAIMessage", It.IsAny<object[]>(), default),
            Times.Once);
    }
}
