using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BaderDiscordBot
{
    class Program
    {
        private DiscordSocketClient _client;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {

            // 🌐 تشغيل خادم ويب مصغر لاستقبال فحص Render والحفاظ على البوت مجانياً
            Task.Run(() =>
{
    try
    {
        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();
        while (true)
        {
            var context = listener.GetContext();
            byte[] response = System.Text.Encoding.UTF8.GetBytes("BaderBot is Alive!");
            context.Response.OutputStream.Write(response, 0, response.Length);
            context.Response.Close();
        }
    }
    catch { }
});


            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
            };

            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;
            _client.UserJoined += OnUserJoinedAsync;

            // 🔑 ضع التوكين الخاص ببوتك بين التنصيص
           // يقرأ التوكين من متغيرات البيئة في Render، وفي حال عدم وجوده يقبل التوكين المحلي
            string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "YOUR_BOT_TOKEN_HERE";

            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();

            Console.WriteLine("⚡ بوت BaderTweaker يعمل الآن بنجاح!");
            await Task.Delay(-1);
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            try
            {
                var role = user.Guild.Roles.FirstOrDefault(r => r.Name.Contains("Verified Client"));

                if (role != null)
                {
                    await user.AddRoleAsync(role);
                    Console.WriteLine($"[+] تم إعطاء الرتبة تلقائياً للـ العضو: {user.Username}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] خطأ في إسناد الرتبة: {ex.Message}");
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}