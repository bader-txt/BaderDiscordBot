using Discord;
using Discord.WebSocket;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BaderDiscordBot.Services;

namespace BaderDiscordBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private AutoRoleService _autoRoleService;
        private TicketService _ticketService;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            StartHttpServer();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMembers |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;
            _client.MessageReceived += OnMessageReceivedAsync;

            // 🔗 تهيئة الخدمات المضافة
            _autoRoleService = new AutoRoleService(_client);
            _ticketService = new TicketService(_client);

            string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "YOUR_BOT_TOKEN_HERE";

            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();

            Console.WriteLine("⚡ بوت BaderDiscordBot يعمل الآن متضمناً خدمة التيكتات والرتب!");
            await Task.Delay(-1);
        }

        private async Task OnMessageReceivedAsync(SocketMessage message)
        {
            if (message.Author.IsBot) return;

            // أمر إرسال بنل التيكتات في القناة الحالية
            if (message.Content.ToLower() == "!setup-ticket")
            {
                if (message.Channel is SocketTextChannel textChannel)
                {
                    await _ticketService.SendTicketPanelAsync(textChannel);
                    await message.DeleteAsync(); // حذف أمر الإرسال لإبقاء الشات نظيفاً
                }
            }
        }

        private static void StartHttpServer()
        {
            Task.Run(() =>
            {
                try
                {
                    string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://*:{port}/");
                    listener.Start();
                    while (true)
                    {
                        var context = listener.GetContext();
                        byte[] response = Encoding.UTF8.GetBytes("BaderBot Service is Alive!");
                        context.Response.OutputStream.Write(response, 0, response.Length);
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebServer Error]: {ex.Message}");
                }
            });
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}