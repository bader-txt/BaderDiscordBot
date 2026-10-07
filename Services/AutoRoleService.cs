using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BaderDiscordBot.Services
{
    public class AutoRoleService
    {
        private readonly DiscordSocketClient _client;

        public AutoRoleService(DiscordSocketClient client)
        {
            _client = client;
            // تسجيل حدث انضمام عضو جديد للسيرفر
            _client.UserJoined += OnUserJoinedAsync;
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            try
            {
                // البحث عن رتبة تحتوي على اسم "Verified Client"
                var role = user.Guild.Roles.FirstOrDefault(r => r.Name.Contains("Verified Client"));

                if (role != null)
                {
                    await user.AddRoleAsync(role);
                    Console.WriteLine($"[AutoRole] [+] تم إسناد الرتبة ({role.Name}) تلقائياً للعضو: {user.Username}");
                }
                else
                {
                    Console.WriteLine($"[AutoRole] [-] لم يتم العثور على رتبة 'Verified Client' في سيرفر: {user.Guild.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoRole] [-] خطأ أثناء إسناد الرتبة للعضو {user.Username}: {ex.Message}");
            }
        }
    }
}